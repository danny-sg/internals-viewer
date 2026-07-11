using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// First consolidation step that folds paired Begin/End lifecycle events into a single event carrying the duration
/// </summary>
/// <remarks>
/// Applies to <c>wait_info</c>, <c>latch_suspend</c>, <c>latch_acquired</c>/<c>latch_released</c> and
/// <c>file_read</c>/<c>file_read_completed</c> pairs; point events pass through untouched.
///
/// The Begin event is kept in place and its <see cref="EngineEvent.DurationUs"/> is filled from the matching End,
/// which is then dropped. Keeping the Begin in position preserves ordering and means any unclosed Begin (a truncated
/// capture) survives as an open interval.
///
/// Capture order is unreliable — an End is frequently buffered ahead of its Begin — so pairing does not scan forward.
/// Instead the Begins and Ends sharing a key (wait_resource for waits, latch address for suspends) are each sorted by
/// time and zipped positionally: the same key is only ever suspended once at a time, so the Nth Begin closes with the
/// Nth End regardless of the order they were captured in.
///
/// Duration is taken from the End's SQL-measured value rather than a timestamp delta, so the fold is immune to the
/// quantised, out-of-order capture timestamps.
/// </remarks>
public static class IntervalCollapser
{
    public static List<EngineEvent> Collapse(IReadOnlyList<EngineEvent> events)
    {
        var dropped = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        // task_address is constant across the whole (serial) query, so the buffer latch address is the key: a suspend
        // carries it as LatchAddress and its wait carries the same value as wait_resource.
        FoldByKey(
            events,
            dropped,
            begin: e => e is WaitEvent { IsEnd: false } w ? w.WaitResource : null,
            end: e => e is WaitEvent { IsEnd: true } w ? w.WaitResource : null);

        FoldByKey(
            events,
            dropped,
            begin: e => e is LatchEvent l && IsSuspendBegin(l) ? l.LatchAddress : null,
            end: e => e is LatchEvent l && IsSuspendEnd(l) ? l.LatchAddress : null);

        // Fold each latch hold: the acquire keeps its place and takes the release's hold duration. A latch is released
        // before it is re-acquired, so per address the acquires and releases pair positionally in time order.
        FoldByKey(
            events,
            dropped,
            begin: e => e is LatchEvent { Name: "latch_acquired" } l ? l.LatchAddress : null,
            end: e => e is LatchEvent { Name: "latch_released" } l ? l.LatchAddress : null);

        // Fold each file read: only the completed carries the size (the page range) and duration, so copy them onto the
        // begin, which keeps its place; the two are paired by file offset. Writes fold the same way.
        FoldByKey(
            events,
            dropped,
            begin: e => e is FileEvent f && !IsCompleted(f) ? (ulong)f.Offset : null,
            end: e => e is FileEvent f && IsCompleted(f) ? (ulong)f.Offset : null,
            onPair: (begin, end) =>
            {
                ((FileEvent)begin).Size = ((FileEvent)end).Size;

                begin.DurationUs = end.DurationUs;
            });

        var result = new List<EngineEvent>(events.Count);

        foreach (var e in events)
        {
            if (!dropped.Contains(e))
            {
                result.Add(e);
            }
        }

        return result;
    }

    // Pairs each Begin with the End sharing its key, matched positionally in time order so capture order is irrelevant,
    // applies onPair (default: copy the End's duration onto the Begin), and marks the End for dropping. Unmatched
    // Begins/Ends are left alone.
    private static void FoldByKey(
        IReadOnlyList<EngineEvent> events,
        HashSet<EngineEvent> dropped,
        Func<EngineEvent, ulong?> begin,
        Func<EngineEvent, ulong?> end,
        Action<EngineEvent, EngineEvent>? onPair = null)
    {
        onPair ??= static (b, e) => b.DurationUs = e.DurationUs;

        var begins = new Dictionary<ulong, List<EngineEvent>>();

        var ends = new Dictionary<ulong, List<EngineEvent>>();

        foreach (var e in events)
        {
            if (begin(e) is { } beginKey)
            {
                Bucket(begins, beginKey).Add(e);
            }
            else if (end(e) is { } endKey)
            {
                Bucket(ends, endKey).Add(e);
            }
        }

        foreach (var (key, beginList) in begins)
        {
            if (!ends.TryGetValue(key, out var endList))
            {
                continue;
            }

            var orderedBegins = beginList.OrderBy(b => b.TimeUs).ToList();

            var orderedEnds = endList.OrderBy(e => e.TimeUs).ToList();

            var pairs = Math.Min(orderedBegins.Count, orderedEnds.Count);

            for (var i = 0; i < pairs; i++)
            {
                onPair(orderedBegins[i], orderedEnds[i]);

                dropped.Add(orderedEnds[i]);
            }
        }
    }

    private static List<EngineEvent> Bucket(Dictionary<ulong, List<EngineEvent>> map, ulong key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];

            map[key] = list;
        }

        return list;
    }

    private static bool IsSuspendBegin(EngineEvent e) => e.Name == "latch_suspend_begin";

    private static bool IsSuspendEnd(EngineEvent e) => e.Name == "latch_suspend_end";

    private static bool IsCompleted(FileEvent f) => f.Name?.Contains("completed") ?? false;
}
