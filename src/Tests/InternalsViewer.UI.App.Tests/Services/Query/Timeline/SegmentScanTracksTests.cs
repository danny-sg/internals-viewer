using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Services.Query.Timeline;

namespace InternalsViewer.UI.App.Tests.Services.Query.Timeline;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class SegmentScanTracksTests
{
    [Fact]
    public void Stacks_Overlapping_Column_Scans_Of_The_Same_Row_Group_Into_Separate_Tracks()
    {
        var tracks = new SegmentScanTracks();

        tracks.Rebuild(
        [
            Scan(rowGroup: 0, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 0, column: 2, timeUs: 110, durationUs: 50),
            Scan(rowGroup: 0, column: 3, timeUs: 120, durationUs: 50),
        ]);

        Assert.Equal(3, tracks.TrackCount);
        Assert.Equal(0, tracks.TrackOf(0));
        Assert.Equal(1, tracks.TrackOf(1));
        Assert.Equal(2, tracks.TrackOf(2));
    }

    [Fact]
    public void Reuses_A_Track_Once_Its_Scan_Has_Ended()
    {
        var tracks = new SegmentScanTracks();

        tracks.Rebuild(
        [
            Scan(rowGroup: 0, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 0, column: 2, timeUs: 110, durationUs: 10),
            Scan(rowGroup: 0, column: 3, timeUs: 130, durationUs: 50),
        ]);

        Assert.Equal(2, tracks.TrackCount);
        Assert.Equal(0, tracks.TrackOf(0));
        Assert.Equal(1, tracks.TrackOf(1));
        Assert.Equal(1, tracks.TrackOf(2));
    }

    [Fact]
    public void Scans_Of_Different_Row_Groups_Share_A_Track_Even_When_They_Overlap()
    {
        var tracks = new SegmentScanTracks();

        tracks.Rebuild(
        [
            Scan(rowGroup: 0, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 1, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 2, column: 1, timeUs: 100, durationUs: 50),
        ]);

        Assert.Equal(1, tracks.TrackCount);
        Assert.Equal(0, tracks.TrackOf(0));
        Assert.Equal(0, tracks.TrackOf(1));
        Assert.Equal(0, tracks.TrackOf(2));
    }

    [Fact]
    public void Orders_Scans_Starting_Together_By_Column()
    {
        var tracks = new SegmentScanTracks();

        tracks.Rebuild(
        [
            Scan(rowGroup: 0, column: 5, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 0, column: 2, timeUs: 100, durationUs: 50),
        ]);

        Assert.Equal(1, tracks.TrackOf(0));
        Assert.Equal(0, tracks.TrackOf(1));
    }

    [Fact]
    public void Ignores_Events_That_Are_Not_Segment_Scans()
    {
        var tracks = new SegmentScanTracks();

        tracks.Rebuild(
        [
            new SegmentEliminateEvent { RowGroupId = 0, TimeUs = 100 },
            new EngineEvent { TimeUs = 100 },
        ]);

        Assert.Equal(1, tracks.TrackCount);
        Assert.Equal(0, tracks.TrackOf(0));
        Assert.Equal(0, tracks.TrackOf(5));
    }

    [Fact]
    public void Min_Band_Height_Grows_With_The_Track_Count()
    {
        var tracks = new SegmentScanTracks();

        tracks.Rebuild(
        [
            Scan(rowGroup: 0, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 0, column: 2, timeUs: 100, durationUs: 50),
        ]);

        Assert.Equal(2 * SegmentScanTracks.MinTrackHeight * 2 + 4, tracks.MinBandHeight(bandPadding: 2));
    }

    private static SegmentScanEvent Scan(long rowGroup, int column, long timeUs, long durationUs) => new()
    {
        NodeId = 1,
        RowGroupId = rowGroup,
        ColumnId = column,
        TimeUs = timeUs,
        DurationUs = durationUs,
        IsScanStart = true,
    };
}
