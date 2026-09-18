using InternalsViewer.Query.Events.BatchMode;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Gives each rowgroup read issued event the span of the rowgroup's work, since the engine raises no read end event
/// </summary>
/// <remarks>
/// Setting up a rowgroup looks its objects up in the pool first, then issues the rowgroup read, then starts the segment
/// scans, so the lookups on a task belong to the next rowgroup read on that task, or failing that to the next segment
/// scan. The read is pulled back to the first of its lookups and runs to the latest segment scan end for the rowgroup on
/// the same task. A read with no segment scans behind it keeps its instant.
/// </remarks>
public static class RowGroupReadSpanner
{
    public static void Apply(IReadOnlyList<EngineEvent> events)
    {
        var open = new Dictionary<(ulong Task, long RowGroup), ColumnStoreScanEvent>();

        var pending = new Dictionary<ulong, List<ObjectPoolEvent>>();

        foreach (var engineEvent in events.OrderBy(e => e.SequenceId))
        {
            var task = engineEvent.TaskKey();

            switch (engineEvent)
            {
                case ObjectPoolEvent pool:
                    if (!pending.TryGetValue(task, out var lookups))
                    {
                        lookups = [];

                        pending[task] = lookups;
                    }

                    lookups.Add(pool);

                    break;

                case ColumnStoreScanEvent { IsRowGroupRead: true, RowGroupId: { } rowGroup } read:
                    open[(task, rowGroup)] = read;

                    pending.Remove(task, out var claimed);

                    foreach (var lookup in claimed ?? [])
                    {
                        if (lookup.RowGroupId == rowGroup && lookup.TimeUs < read.TimeUs)
                        {
                            read.DurationUs += read.TimeUs - lookup.TimeUs;
                            read.TimeUs = lookup.TimeUs;
                        }
                    }

                    break;

                case SegmentScanEvent scan:
                    pending.Remove(task);

                    if (open.TryGetValue((task, scan.RowGroupId), out var owner))
                    {
                        var end = scan.TimeUs + scan.DurationUs;

                        if (end > owner.TimeUs + owner.DurationUs)
                        {
                            owner.DurationUs = end - owner.TimeUs;
                        }
                    }

                    break;
            }
        }
    }
}
