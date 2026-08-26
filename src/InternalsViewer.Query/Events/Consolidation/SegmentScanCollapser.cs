using InternalsViewer.Query.Events.BatchMode;

namespace InternalsViewer.Query.Events.Consolidation;

public static class SegmentScanCollapser
{
    public static List<EngineEvent> Collapse(IReadOnlyList<EngineEvent> events)
    {
        var starts = new Dictionary<(int Node, long RowGroup, int Column, int Thread), Queue<SegmentScanEvent>>();

        foreach (var scan in events.OfType<SegmentScanEvent>().Where(s => s.IsScanStart))
        {
            if (!starts.TryGetValue(KeyOf(scan), out var queue))
            {
                queue = new Queue<SegmentScanEvent>();

                starts[KeyOf(scan)] = queue;
            }

            queue.Enqueue(scan);
        }

        var folded = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        foreach (var scan in events.OfType<SegmentScanEvent>().Where(s => !s.IsScanStart))
        {
            if (!starts.TryGetValue(KeyOf(scan), out var queue) || queue.Count == 0)
            {
                continue;
            }

            var start = queue.Dequeue();

            start.InputRows = scan.InputRows;
            start.OutputRows = scan.OutputRows;
            start.PureRowBuckets = scan.PureRowBuckets;
            start.ImpureRowBuckets = scan.ImpureRowBuckets;
            start.DurationUs = scan.DurationUs > 0 ? scan.DurationUs : Math.Max(0, scan.TimeUs - start.TimeUs);
            start.FoldedFrom = scan;

            folded.Add(scan);
        }

        return [.. events.Where(e => !folded.Contains(e))];
    }

    private static (int, long, int, int) KeyOf(SegmentScanEvent scan)
        => (scan.NodeId, scan.RowGroupId, scan.ColumnId, scan.ThreadId);
}
