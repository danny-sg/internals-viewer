using InternalsViewer.Query.Events.Latches;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Consolidation step that collapses a burst of identical buffer latches on one page into a single event
/// </summary>
/// <remarks>
/// A scan touching every row on a page re-latches the page's buffer (BUF) structure many times in the same instant, so
/// the quantised capture shows a run of identical <c>latch_acquired</c> on the same page at the same timestamp. They are one page visit,
/// not many reads — left separate, SpreadEvents lays the run end-to-end and smears these identical same-page 
/// events forward across the axis. A run of acquires sharing (page, class, mode) within <see cref="CoalesceGapUs"/> is folded into its
/// earliest, whose hold is extended to span the whole visit.
///
/// Runs on the collapsed stream (after <see cref="IntervalCollapser"/> folds acquire/release) and before <see cref="ReaderGrouper"/>, so
/// the read grouping then forms one buffer-pool read per page visit.
/// </remarks>
public static class BufferLatchCoalescing
{
    // Buffer accesses within this gap on one page are the same visit; a genuine later re-read (further apart) stays
    // separate, so a page read twice in a query still shows as two reads.
    private const long CoalesceGapUs = 2_000;

    public static List<EngineEvent> Coalesce(IReadOnlyList<EngineEvent> events)
    {
        var dropped = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        var runs = events.OfType<LatchEvent>()
                         .Where(IsBufferAcquire)
                         .GroupBy(l => (l.PageAddress!.Value, l.LatchClass, l.LatchMode));

        foreach (var run in runs)
        {
            LatchEvent? head = null;

            var tail = default(EngineEvent);

            var end = 0L;

            foreach (var latch in run.OrderBy(l => l.TimeUs))
            {
                if (head is null || latch.TimeUs > end + CoalesceGapUs)
                {
                    head = latch;

                    tail = TailOf(latch);

                    end = latch.TimeUs + latch.DurationUs;

                    continue;
                }

                // Fold this acquire into the run head — extend the head's hold to cover the visit, drop the duplicate.
                end = Math.Max(end, latch.TimeUs + latch.DurationUs);

                head.DurationUs = end - head.TimeUs;

                tail!.FoldedFrom = latch;

                tail = TailOf(latch);

                dropped.Add(latch);
            }
        }

        if (dropped.Count == 0)
        {
            return events as List<EngineEvent> ?? [.. events];
        }

        var result = new List<EngineEvent>(events.Count - dropped.Count);

        foreach (var e in events)
        {
            if (!dropped.Contains(e))
            {
                result.Add(e);
            }
        }

        return result;
    }

    private static bool IsBufferAcquire(LatchEvent l) =>
        l is { LatchClass: LatchClass.BUF, Name: "latch_acquired", PageAddress: not null };

    // The last link of an event's fold chain — each folded acquire already owns its release through FoldedFrom, so appending
    // at the tail nests the chain (head -> release -> duplicate -> its release ...) instead of overwriting a link
    private static EngineEvent TailOf(EngineEvent e)
    {
        while (e.FoldedFrom is { } folded)
        {
            e = folded;
        }

        return e;
    }
}
