using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Assigns each segment scan a lane within the Segment Scan row so column scans of the same row group that overlap in
/// time stack instead of drawing over one another
/// </summary>
/// <remarks>
/// Lanes are packed per row group. A scan takes the lowest lane whose previous scan of that row group has ended, so a row group's column
/// scans running side by side each get their own lane while scans of different row groups share lanes freely. The lane count is the widest
/// row group, which sets the row's minimum height.
/// </remarks>
internal sealed class SegmentScanLanes
{
    public const float MinLaneHeight = 6f;

    private int[] _lanes = [];

    public int LaneCount { get; private set; } = 1;

    public int LaneOf(int eventIndex) => eventIndex >= 0 && eventIndex < _lanes.Length ? _lanes[eventIndex] : 0;

    public float MinRowHeight(float rowPadding) => LaneCount * MinLaneHeight * 2 + rowPadding * 2;

    public void Rebuild(IReadOnlyList<EngineEvent> events)
    {
        _lanes = new int[events.Count];

        LaneCount = 1;

        var rowGroups = new Dictionary<(int NodeId, long RowGroupId), List<int>>();

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is not SegmentScanEvent scan)
            {
                continue;
            }

            var key = (scan.NodeId, scan.RowGroupId);

            if (!rowGroups.TryGetValue(key, out var indexes))
            {
                indexes = [];

                rowGroups[key] = indexes;
            }

            indexes.Add(i);
        }

        var laneEnds = new List<long>();

        foreach (var indexes in rowGroups.Values)
        {
            indexes.Sort((a, b) => CompareScans((SegmentScanEvent)events[a], (SegmentScanEvent)events[b]));

            laneEnds.Clear();

            foreach (var index in indexes)
            {
                var scan = events[index];

                var lane = FreeLane(laneEnds, scan.TimeUs);

                _lanes[index] = lane;

                laneEnds[lane] = scan.TimeUs + scan.DurationUs;
            }

            if (laneEnds.Count > LaneCount)
            {
                LaneCount = laneEnds.Count;
            }
        }
    }

    private static int CompareScans(SegmentScanEvent a, SegmentScanEvent b)
    {
        var byTime = a.TimeUs.CompareTo(b.TimeUs);

        return byTime != 0 ? byTime : a.ColumnId.CompareTo(b.ColumnId);
    }

    private static int FreeLane(List<long> laneEnds, long start)
    {
        for (var lane = 0; lane < laneEnds.Count; lane++)
        {
            if (laneEnds[lane] <= start)
            {
                return lane;
            }
        }

        laneEnds.Add(start);

        return laneEnds.Count - 1;
    }
}
