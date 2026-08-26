using InternalsViewer.Query.Events.BatchMode;

namespace InternalsViewer.Query.Events.Consolidation;

public static class SegmentScanCollapser
{
    public static List<EngineEvent> Collapse(IReadOnlyList<EngineEvent> events)
    {
        var started = new Dictionary<(int Node, long RowGroup, int Column, int Thread), SegmentScanEvent>();

        var result = new List<EngineEvent>(events.Count);

        foreach (var engineEvent in events)
        {
            if (engineEvent is not SegmentScanEvent scan)
            {
                result.Add(engineEvent);

                continue;
            }

            var key = (scan.NodeId, scan.RowGroupId, scan.ColumnId, scan.ThreadId);

            if (scan.Name.EndsWith("started", StringComparison.Ordinal))
            {
                started[key] = scan;

                result.Add(scan);

                continue;
            }

            if (!started.Remove(key, out var begin))
            {
                result.Add(scan);

                continue;
            }

            begin.Name = "Columnstore segment scan";
            begin.InputRows = scan.InputRows;
            begin.OutputRows = scan.OutputRows;
            begin.PureRowBuckets = scan.PureRowBuckets;
            begin.ImpureRowBuckets = scan.ImpureRowBuckets;
            begin.DurationUs = scan.DurationUs > 0 ? scan.DurationUs : Math.Max(0, scan.TimeUs - begin.TimeUs);
            begin.FoldedFrom = scan;
        }

        return result;
    }
}
