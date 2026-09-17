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
    private const string CachingDecision = "large_cache_caching_decision";

    public static void Stamp(IReadOnlyList<EngineEvent> events)
    {
        var firstReads = new Dictionary<(ulong Task, PlanNodeIdentifier? Node), EngineEvent>();

        foreach (var engineEvent in events.OrderBy(e => e.SequenceId))
        {
            var task = engineEvent.TaskAddress ?? engineEvent.WorkerAddress ?? (ulong)engineEvent.ThreadId;

            switch (engineEvent)
            {
                case ObjectPoolEvent { IsHit: false } miss:
                    if (FirstReadFor(firstReads, task, miss) is { } read && read.TimeUs < miss.TimeUs)
                    {
                        miss.DurationUs = miss.TimeUs - read.TimeUs;
                        miss.TimeUs = read.TimeUs;
                        miss.Timestamp = read.Timestamp;
                    }

                    ClearTask(firstReads, task);

                    break;

                case ColumnStoreScanEvent { EventName: CachingDecision }:
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
