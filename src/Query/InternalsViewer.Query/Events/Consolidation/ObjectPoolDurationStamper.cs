using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Infers duration for object pool events
/// </summary>
/// <remarks>
/// Object Pool miss events do not have duration and fire after the object has been built.
///
/// This infers the duration based on the fact that any preceding read activity on the columnstore index since the last columnstore event on
/// the same task will be caused by a cold read of the LOB pages.
///
/// Reads are matched to plan node + task, then the first read is found on the match to give a start time.
///
/// Start Time = First matched read start time
/// Duration   = Miss event start time - matched read start time
///
/// Non miss events manage the build of the first read reference collection.
///
/// Caching decisions appear mid read so are excluded from the reference build/reset process.
///
/// A miss without a plan node falls back to any read on the same task.
/// </remarks>
public static class ObjectPoolDurationStamper
{
    public static void Stamp(IReadOnlyList<EngineEvent> events)
    {
        var firstReads = new Dictionary<(ulong Task, PlanNodeIdentifier? Node), EngineEvent>();

        var linkedSpans = LinkedSpans(events);

        foreach (var engineEvent in events.OrderBy(e => e.SequenceId))
        {
            var task = engineEvent.TaskKey();

            switch (engineEvent)
            {
                case ObjectPoolEvent { IsHit: false } miss:
                    var end = miss.TimeUs;

                    EngineEvent? first;

                    if (linkedSpans.TryGetValue(miss, out var span))
                    {
                        first = span.First;

                        end = Math.Max(end, span.EndUs);
                    }
                    else
                    {
                        first = miss.Pages.Count > 0 ? null : FirstReadFor(firstReads, task, miss);
                    }

                    if (first is { } read)
                    {
                        var start = Math.Min(read.TimeUs, miss.TimeUs);

                        if (end > start)
                        {
                            if (read.TimeUs < miss.TimeUs)
                            {
                                miss.Timestamp = read.Timestamp;
                            }

                            miss.TimeUs = start;
                            miss.DurationUs = end - start;
                        }
                    }

                    ClearTask(firstReads, task);

                    break;

                case ColumnStoreScanEvent { IsRowGroupReadAhead: true }:
                case ReadEventGroup { IsReadAhead: true }:
                case ReadEventGroup { IsAllocationPage: true }:
                    break;

                case ObjectPoolEvent or ColumnStoreScanEvent or SegmentScanEvent or SegmentEliminateEvent:
                    ClearTask(firstReads, task);

                    break;

                case ReadEventGroup or IoEvent or FileEvent or LatchEvent:
                    firstReads.TryAdd((task, engineEvent.PlanNodeIdentifier), engineEvent);

                    break;
            }
        }
    }

    private static Dictionary<ObjectPoolEvent, (EngineEvent First, long EndUs)> LinkedSpans(IReadOnlyList<EngineEvent> events)
    {
        var spans = new Dictionary<ObjectPoolEvent, (EngineEvent First, long EndUs)>(ReferenceEqualityComparer.Instance);

        foreach (var read in events.OfType<ReadEventGroup>())
        {
            if (read.PoolLookup is not { } lookup || read.IsReadAhead || read.SequenceId > lookup.SequenceId)
            {
                continue;
            }

            var readEnd = read.TimeUs + read.DurationUs;

            spans[lookup] = spans.TryGetValue(lookup, out var current)
                ? (read.TimeUs < current.First.TimeUs ? read : current.First, Math.Max(current.EndUs, readEnd))
                : (read, readEnd);
        }

        return spans;
    }

    private static EngineEvent? FirstReadFor(Dictionary<(ulong Task, PlanNodeIdentifier? Node), EngineEvent> firstReads,
                                             ulong task,
                                             ObjectPoolEvent miss)
    {
        if (firstReads.TryGetValue((task, miss.PlanNodeIdentifier), out var read))
        {
            return read;
        }

        if (miss.PlanNodeIdentifier is not null)
        {
            return null;
        }

        return firstReads.Where(pair => pair.Key.Task == task).Select(pair => pair.Value).MinBy(r => r.TimeUs);
    }

    private static void ClearTask(Dictionary<(ulong Task, PlanNodeIdentifier? Node), EngineEvent> firstReads, ulong task)
    {
        foreach (var key in firstReads.Keys.Where(k => k.Task == task).ToList())
        {
            firstReads.Remove(key);
        }
    }
}
