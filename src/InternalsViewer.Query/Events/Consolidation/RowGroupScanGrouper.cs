using InternalsViewer.Query.Events.BatchMode;

namespace InternalsViewer.Query.Events.Consolidation;

public static class RowGroupScanGrouper
{
    public static List<EngineEvent> Group(List<EngineEvent> events)
    {
        List<EngineEvent> work = [.. events.Where(IsRowGroupWork).OrderBy(e => e.SequenceId)];

        List<ColumnStoreScanEvent> finishes =
        [
            .. work.OfType<ColumnStoreScanEvent>().Where(e => e is { IsRowGroupFinished: true, RowGroupId: not null })
        ];

        if (finishes.Count == 0)
        {
            return events;
        }

        var members = finishes.ToDictionary(f => f, _ => new List<EngineEvent>(), ReferenceEqualityComparer.Instance);

        var byRowGroup = finishes.ToLookup(f => f.RowGroupId!.Value);

        var byTask = finishes.ToLookup(f => f.TaskKey());

        foreach (var engineEvent in work)
        {
            var candidates = RowGroupOf(engineEvent) is { } rowGroup ? byRowGroup[rowGroup] : byTask[engineEvent.TaskKey()];

            if (OwnerOf(engineEvent, candidates) is { } owner)
            {
                members[owner].Add(engineEvent);
            }
        }

        var groupOf = new Dictionary<EngineEvent, RowGroupScanEvent>(ReferenceEqualityComparer.Instance);

        foreach (var finished in finishes)
        {
            var owned = members[finished];

            var group = Build(finished.RowGroupId!.Value, owned, finished);

            foreach (var member in owned)
            {
                groupOf[member] = group;
            }
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

        var thread = owned.OfType<SegmentScanEvent>().Select(s => s.ThreadId).FirstOrDefault(id => id != 0);

        if (thread != 0)
        {
            foreach (var member in owned.Where(m => m.ThreadId == 0))
            {
                member.ThreadId = thread;
            }
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
            ThreadId = thread != 0 ? thread : finished.ThreadId,
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

    private static ColumnStoreScanEvent? OwnerOf(EngineEvent engineEvent, IEnumerable<ColumnStoreScanEvent> candidates)
    {
        List<ColumnStoreScanEvent> following =
        [
            .. candidates.Where(f => f.SequenceId >= engineEvent.SequenceId && SameNode(engineEvent, f))
        ];

        var task = engineEvent.TaskKey();

        return following.FirstOrDefault(f => f.TaskKey() == task) ?? following.FirstOrDefault();
    }

    private static bool SameNode(EngineEvent engineEvent, ColumnStoreScanEvent finished)
        => engineEvent.PlanNodeIdentifier is null
           || finished.PlanNodeIdentifier is null
           || engineEvent.PlanNodeIdentifier == finished.PlanNodeIdentifier;

    private static long? RowGroupOf(EngineEvent engineEvent) => engineEvent switch
    {
        ObjectPoolEvent pool => pool.RowGroupId,
        SegmentScanEvent scan => scan.RowGroupId,
        ColumnStoreScanEvent scan => scan.RowGroupId,
        ColumnstoreFilterEvent filter => filter.RowGroupId,
        _ => null
    };

    private static bool IsRowGroupWork(EngineEvent engineEvent) => engineEvent switch
    {
        ColumnStoreScanEvent { IsRowGroupReadAhead: true } or ColumnStoreScanEvent { IsBitmapFilterSet: true } => false,
        ObjectPoolEvent or SegmentScanEvent or ColumnStoreScanEvent or ColumnstoreFilterEvent => true,
        _ => false
    };
}
