using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.UI.App.Controls.Timeline;

namespace InternalsViewer.UI.App.Tests.Controls.Timeline;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class SegmentScanLanesTests
{
    [Fact]
    public void Stacks_Overlapping_Column_Scans_Of_The_Same_Row_Group_Into_Separate_Lanes()
    {
        var lanes = new SegmentScanLanes();

        lanes.Rebuild(
        [
            Scan(rowGroup: 0, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 0, column: 2, timeUs: 110, durationUs: 50),
            Scan(rowGroup: 0, column: 3, timeUs: 120, durationUs: 50),
        ]);

        Assert.Equal(3, lanes.LaneCount);
        Assert.Equal(0, lanes.LaneOf(0));
        Assert.Equal(1, lanes.LaneOf(1));
        Assert.Equal(2, lanes.LaneOf(2));
    }

    [Fact]
    public void Reuses_A_Lane_Once_Its_Scan_Has_Ended()
    {
        var lanes = new SegmentScanLanes();

        lanes.Rebuild(
        [
            Scan(rowGroup: 0, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 0, column: 2, timeUs: 110, durationUs: 10),
            Scan(rowGroup: 0, column: 3, timeUs: 130, durationUs: 50),
        ]);

        Assert.Equal(2, lanes.LaneCount);
        Assert.Equal(0, lanes.LaneOf(0));
        Assert.Equal(1, lanes.LaneOf(1));
        Assert.Equal(1, lanes.LaneOf(2));
    }

    [Fact]
    public void Scans_Of_Different_Row_Groups_Share_A_Lane_Even_When_They_Overlap()
    {
        var lanes = new SegmentScanLanes();

        lanes.Rebuild(
        [
            Scan(rowGroup: 0, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 1, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 2, column: 1, timeUs: 100, durationUs: 50),
        ]);

        Assert.Equal(1, lanes.LaneCount);
        Assert.Equal(0, lanes.LaneOf(0));
        Assert.Equal(0, lanes.LaneOf(1));
        Assert.Equal(0, lanes.LaneOf(2));
    }

    [Fact]
    public void Orders_Scans_Starting_Together_By_Column()
    {
        var lanes = new SegmentScanLanes();

        lanes.Rebuild(
        [
            Scan(rowGroup: 0, column: 5, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 0, column: 2, timeUs: 100, durationUs: 50),
        ]);

        Assert.Equal(1, lanes.LaneOf(0));
        Assert.Equal(0, lanes.LaneOf(1));
    }

    [Fact]
    public void Ignores_Events_That_Are_Not_Segment_Scans()
    {
        var lanes = new SegmentScanLanes();

        lanes.Rebuild(
        [
            new SegmentEliminateEvent { RowGroupId = 0, TimeUs = 100 },
            new EngineEvent { TimeUs = 100 },
        ]);

        Assert.Equal(1, lanes.LaneCount);
        Assert.Equal(0, lanes.LaneOf(0));
        Assert.Equal(0, lanes.LaneOf(5));
    }

    [Fact]
    public void Min_Row_Height_Grows_With_The_Lane_Count()
    {
        var lanes = new SegmentScanLanes();

        lanes.Rebuild(
        [
            Scan(rowGroup: 0, column: 1, timeUs: 100, durationUs: 50),
            Scan(rowGroup: 0, column: 2, timeUs: 100, durationUs: 50),
        ]);

        Assert.Equal(2 * SegmentScanLanes.MinLaneHeight * 2 + 4, lanes.MinRowHeight(rowPadding: 2));
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
