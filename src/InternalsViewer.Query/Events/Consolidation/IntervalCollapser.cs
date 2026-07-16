using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Waits;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// First consolidation step that folds paired Begin/End lifecycle events into a single event carrying the duration
/// </summary>
/// <remarks>
/// Applies to:
///     wait_info
///     latch_suspend
///     latch_acquired
///     latch_released
///     lock_acquired
///     lock_released
///     file_read
///     file_read_completed
///
/// The Begin event is kept in place and its <see cref="EngineEvent.DurationUs"/> is filled from the matching End, which is then dropped.
/// Keeping the Begin in position preserves ordering and means any unclosed Begin (a truncated capture) survives as an open interval.
///
/// Capture order is unreliable — an End is frequently buffered ahead of its Begin — so pairing does not scan forward. Instead the Begins
/// and Ends sharing a key are each sorted by time and zipped positionally: the Nth Begin closes with the Nth End regardless of the order
/// they were captured in. That only holds if the key identifies a series that is open once at a time, so the key carries whatever
/// disambiguates concurrent intervals - parallel workers wait on the same resource (or hold the same shared latch) simultaneously, so
/// waits and latches key on the owning task as well as the resource; file offsets repeat across database files, so file events key on
/// file, offset and direction; locks key on the full resource identity.
///
/// Duration is taken from the End's SQL-measured value rather than a timestamp delta, so the fold is immune to the quantised, out-of-order
/// capture timestamps.
/// </remarks>
public static class IntervalCollapser
{
    public static List<EngineEvent> Collapse(IReadOnlyList<EngineEvent> events)
    {
        var dropped = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        // Several parallel workers wait on (or hold, in a shared mode) the same resource at once, so the owning task is part of the key —
        // per resource alone, overlapping intervals cross-pair and each worker takes the other's measured duration.
        FoldByKey<(ulong TaskAddress, ulong WaitResource)>(
            events,
            dropped,
            begin: e => e is WaitEvent { IsEnd: false, WaitResource: { } resource } w ? (w.TaskAddress ?? 0, resource) : null,
            end: e => e is WaitEvent { IsEnd: true, WaitResource: { } resource } w ? (w.TaskAddress ?? 0, resource) : null);

        FoldByKey<(ulong TaskAddress, ulong LatchAddress)>(
            events,
            dropped,
            begin: e => e is LatchEvent { LatchAddress: { } address } l && IsSuspendBegin(l) ? (l.TaskAddress ?? 0, address) : null,
            end: e => e is LatchEvent { LatchAddress: { } address } l && IsSuspendEnd(l) ? (l.TaskAddress ?? 0, address) : null);

        // Fold latch holds
        //
        // The acquire keeps its place and takes the release's hold duration. A task releases a latch before it re-acquires it, so per task
        // per address the acquires and releases pair positionally in time order.
        FoldByKey<(ulong TaskAddress, ulong LatchAddress)>(
            events,
            dropped,
            begin: e => e is LatchEvent { Name: "latch_acquired", LatchAddress: { } address } l ? (l.TaskAddress ?? 0, address) : null,
            end: e => e is LatchEvent { Name: "latch_released", LatchAddress: { } address } l ? (l.TaskAddress ?? 0, address) : null);

        // Fold lock holds
        //
        // The acquire keeps its place and the release folds in, becoming a single "Lock" that is no longer an acquire/release. Prefer the
        // release's own measured duration, but lock events tend to report it as 0, so fall back to the elapsed hold (release time less
        // acquire time). Paired per resource in time order.
        FoldByKey<LockIdentity>(
            events,
            dropped,
            begin: e => e is LockEvent { Name: "lock_acquired" } l ? l.Identity : null,
            end: e => e is LockEvent { Name: "lock_released" } l ? l.Identity : null,
            onPair: (begin, end) =>
            {
                begin.Name = "Lock";

                begin.DurationUs = end.DurationUs > 0 ? end.DurationUs : Math.Max(0, end.TimeUs - begin.TimeUs);
            });

        // Fold file read
        //
        // Only the completed carries the size (the page range) and duration, so copy them onto the begin, which keeps its place. Offsets
        // repeat across database files, and a write completion can land on an offset just read, so the pair is keyed by file, offset and
        // direction. Writes fold the same way when both ends are captured.
        FoldByKey<(short FileId, long Offset, bool IsRead)>(
            events,
            dropped,
            begin: e => e is FileEvent f && !IsCompleted(f) ? (f.FileId, f.Offset, f.IsRead) : null,
            end: e => e is FileEvent f && IsCompleted(f) ? (f.FileId, f.Offset, f.IsRead) : null,
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

    private static void FoldByKey<TKey>(IReadOnlyList<EngineEvent> events,
                                  HashSet<EngineEvent> dropped,
                                  Func<EngineEvent, TKey?> begin,
                                  Func<EngineEvent, TKey?> end,
                                  Action<EngineEvent, EngineEvent>? onPair = null)
        where TKey : struct
    {
        onPair ??= static (b, e) => b.DurationUs = e.DurationUs;

        var begins = new Dictionary<TKey, List<EngineEvent>>();

        var ends = new Dictionary<TKey, List<EngineEvent>>();

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

                // The Begin owns the End it consumed. The End leaves the stream but its call stack is still this event's work, so anything
                // scoping by the surviving events (the crop's call-stack keep set) can find it — otherwise the End's frames are pruned and
                // the tree loses every release/completion path.
                orderedBegins[i].FoldedFrom = orderedEnds[i];

                dropped.Add(orderedEnds[i]);
            }
        }
    }

    private static List<EngineEvent> Bucket<TKey>(Dictionary<TKey, List<EngineEvent>> map, TKey key)
        where TKey : struct
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
