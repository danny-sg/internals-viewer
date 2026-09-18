using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;

namespace InternalsViewer.UI.App.Services.Query.Timeline;

/// <summary>
/// Assigns each segment scan a lane within the Segment Scan row so column scans of the same row group that overlap in
/// time stack instead of drawing over one another
/// </summary>
/// <remarks>
/// Lanes are packed per row group. A scan takes the lowest lane whose previous scan of that row group has ended, so a row group's column
/// scans running side by side each get their own lane while scans of different row groups share lanes freely. The lane count is the widest
/// row group, which sets the row's minimum height.
/// </remarks>
internal sealed class SegmentScanTracks
{
    public const float MinTrackHeight = 6f;

    private int[] _tracks = [];

    public int TrackCount { get; private set; } = 1;

    public int TrackOf(int eventIndex) => eventIndex >= 0 && eventIndex < _tracks.Length ? _tracks[eventIndex] : 0;

    public static int FreeTrack(List<long> trackEnds, long start)
    {
        for (var track = 0; track < trackEnds.Count; track++)
        {
            if (trackEnds[track] <= start)
            {
                return track;
            }
        }

        trackEnds.Add(start);

        return trackEnds.Count - 1;
    }

    public void Rebuild(IReadOnlyList<EngineEvent> events)
    {
        _tracks = new int[events.Count];

        TrackCount = 1;

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

        var trackEnds = new List<long>();

        foreach (var indexes in rowGroups.Values)
        {
            indexes.Sort((a, b) => CompareScans((SegmentScanEvent)events[a], (SegmentScanEvent)events[b]));

            trackEnds.Clear();

            foreach (var index in indexes)
            {
                var scan = events[index];

                var track = FreeTrack(trackEnds, scan.TimeUs);

                _tracks[index] = track;

                trackEnds[track] = scan.TimeUs + scan.DurationUs;
            }

            if (trackEnds.Count > TrackCount)
            {
                TrackCount = trackEnds.Count;
            }
        }
    }

    private static int CompareScans(SegmentScanEvent a, SegmentScanEvent b)
    {
        var byTime = a.TimeUs.CompareTo(b.TimeUs);

        return byTime != 0 ? byTime : a.ColumnId.CompareTo(b.ColumnId);
    }
}
