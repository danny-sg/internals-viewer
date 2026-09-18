using InternalsViewer.Query.Events.BatchMode;

namespace InternalsViewer.Query.Events.Consolidation;

public static class SegmentScanCollapser
{
    public static List<EngineEvent> Collapse(IReadOnlyList<EngineEvent> events)
    {
        var starts = new Dictionary<(int Node, int Column, int Thread), Queue<SegmentScanEvent>>();

        var folded = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        foreach (var scan in events.OfType<SegmentScanEvent>().OrderBy(s => s.SequenceId))
        {
            if (scan.IsScanStart)
            {
                if (!starts.TryGetValue(KeyOf(scan), out var opened))
                {
                    opened = new Queue<SegmentScanEvent>();

                    starts[KeyOf(scan)] = opened;
                }

                opened.Enqueue(scan);

                continue;
            }

            if (!starts.TryGetValue(KeyOf(scan), out var queue) || queue.Count == 0)
            {
                continue;
            }

            var start = queue.Dequeue();

            start.RowGroupId = scan.RowGroupId;
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

    private static (int, int, int) KeyOf(SegmentScanEvent scan)
        => (scan.NodeId, scan.ColumnId, scan.ThreadId);
}
