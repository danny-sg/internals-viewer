using InternalsViewer.Query.Events.BatchMode;

namespace InternalsViewer.Query.Events.Consolidation;

public static class RowGroupScanGrouper
{
    public static List<EngineEvent> Group(List<EngineEvent> events)
    {
        var groupOf = new Dictionary<EngineEvent, RowGroupScanEvent>(ReferenceEqualityComparer.Instance);

        foreach (var task in events.Where(IsRowGroupWork).GroupBy(e => e.TaskKey()))
        {
            var pending = new List<EngineEvent>();

            foreach (var engineEvent in task.OrderBy(e => e.SequenceId))
            {
                pending.Add(engineEvent);

                if (engineEvent is not ColumnStoreScanEvent { IsRowGroupFinished: true, RowGroupId: { } rowGroup } finished)
                {
                    continue;
                }

                List<EngineEvent> owned = [.. pending.Where(e => BelongsTo(e, rowGroup))];

                var group = Build(rowGroup, owned, finished);

                foreach (var member in owned)
                {
                    groupOf[member] = group;
                }

                pending.Clear();
            }
        }

        if (groupOf.Count == 0)
        {
            return events;
        }

        var emitted = new HashSet<RowGroupScanEvent>(ReferenceEqualityComparer.Instance);

        var result = new List<EngineEvent>(events.Count);

        foreach (var engineEvent in events)
        {
            if (!groupOf.TryGetValue(engineEvent, out var group))
            {
                result.Add(engineEvent);
            }
            else if (emitted.Add(group))
            {
                result.Add(group);
            }
        }

        return result;
    }

    public static void Fit(IReadOnlyList<EngineEvent> events)
    {
        foreach (var group in events.OfType<RowGroupScanEvent>())
        {
            ContainFilters(group.Events);

            var start = group.Events.Min(e => e.TimeUs);

            var end = group.Events.Max(e => e.TimeUs + e.DurationUs);

            foreach (var finished in group.Events.OfType<ColumnStoreScanEvent>().Where(e => e.IsRowGroupFinished && e.TimeUs < end))
            {
                finished.TimeUs = end;
            }

            group.TimeUs = start;

            group.DurationUs = end - start;
        }
    }

    public static List<EngineEvent> Flatten(IReadOnlyList<EngineEvent> events)
        => [.. events.SelectMany(e => e is RowGroupScanEvent group ? group.Events : [e])];

    private static RowGroupScanEvent Build(long rowGroup, List<EngineEvent> owned, ColumnStoreScanEvent finished)
    {
        var node = owned.OfType<SegmentScanEvent>().Select(s => s.PlanNodeIdentifier).FirstOrDefault(id => id is not null)
                   ?? owned.Select(e => e.PlanNodeIdentifier).FirstOrDefault(id => id is not null);

        foreach (var member in owned)
        {
            member.PlanNodeIdentifier ??= node;
        }

        var first = owned.MinBy(e => e.TimeUs)!;

        return new RowGroupScanEvent
        {
            Events = owned,
            RowGroupId = rowGroup,
            SequenceId = owned.Min(e => e.SequenceId),
            Timestamp = first.Timestamp,
            TimeUs = first.TimeUs,
            DatabaseId = finished.DatabaseId,
            ThreadId = finished.ThreadId,
            TaskAddress = finished.TaskAddress,
            WorkerAddress = finished.WorkerAddress,
            PlanNodeIdentifier = node
        };
    }

    private static void ContainFilters(IReadOnlyList<EngineEvent> owned)
    {
        List<SegmentScanEvent> scans = [.. owned.OfType<SegmentScanEvent>()];

        if (scans.Count == 0)
        {
            return;
        }

        foreach (var filter in owned.OfType<ColumnstoreFilterEvent>())
        {
            List<SegmentScanEvent> parents = [.. scans.Where(s => s.ColumnId == filter.ColumnId)];

            if (parents.Count == 0)
            {
                parents = scans;
            }

            var start = parents.Min(s => s.TimeUs);

            if (filter.TimeUs < start)
            {
                filter.TimeUs = start;
            }

            var end = filter.TimeUs + filter.DurationUs;

            foreach (var parent in parents)
            {
                if (parent.TimeUs + parent.DurationUs < end)
                {
                    parent.DurationUs = end - parent.TimeUs;
                }
            }
        }
    }

    private static bool BelongsTo(EngineEvent engineEvent, long rowGroup) => engineEvent switch
    {
        ObjectPoolEvent pool => pool.RowGroupId is null || pool.RowGroupId == rowGroup,
        SegmentScanEvent scan => scan.RowGroupId == rowGroup,
        ColumnStoreScanEvent { IsBitmapFilterSet: true } => false,
        ColumnStoreScanEvent scan => scan.RowGroupId is null || scan.RowGroupId == rowGroup,
        ColumnstoreFilterEvent filter => filter.RowGroupId is null || filter.RowGroupId == rowGroup,
        _ => false
    };

    private static bool IsRowGroupWork(EngineEvent engineEvent)
        => engineEvent is ObjectPoolEvent or SegmentScanEvent or ColumnStoreScanEvent or ColumnstoreFilterEvent;
}
