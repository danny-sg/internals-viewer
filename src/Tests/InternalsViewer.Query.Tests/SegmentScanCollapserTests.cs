using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Consolidation;

namespace InternalsViewer.Query.Tests;

public class SegmentScanCollapserTests
{
    [Fact]
    public void Collapse_Pairs_A_Start_With_The_Next_Finish_And_Takes_The_Finishs_Rowgroup()
    {
        var firstStart = Scan(isStart: true, rowGroup: 0, sequence: 1, timeUs: 1000);

        var firstFinish = Scan(isStart: false, rowGroup: 1, sequence: 2, timeUs: 1400, outputRows: 10);

        var secondStart = Scan(isStart: true, rowGroup: 1, sequence: 3, timeUs: 9000);

        var secondFinish = Scan(isStart: false, rowGroup: 0, sequence: 4, timeUs: 9300, outputRows: 20);

        var collapsed = SegmentScanCollapser.Collapse(new EngineEvent[] { firstStart, firstFinish, secondStart, secondFinish });

        Assert.Equal([firstStart, secondStart], collapsed);
        Assert.Equal(1, firstStart.RowGroupId);
        Assert.Equal(400, firstStart.DurationUs);
        Assert.Equal(10, firstStart.OutputRows);
        Assert.Equal(0, secondStart.RowGroupId);
        Assert.Equal(300, secondStart.DurationUs);
        Assert.Equal(20, secondStart.OutputRows);
    }

    [Fact]
    public void Collapse_Keeps_Columns_And_Threads_Apart()
    {
        var columnStart = Scan(isStart: true, rowGroup: 0, sequence: 1, timeUs: 1000, column: 3);

        var otherColumnStart = Scan(isStart: true, rowGroup: 0, sequence: 2, timeUs: 1000, column: 4);

        var otherColumnFinish = Scan(isStart: false, rowGroup: 0, sequence: 3, timeUs: 1200, column: 4);

        var collapsed = SegmentScanCollapser.Collapse(new EngineEvent[] { columnStart, otherColumnStart, otherColumnFinish });

        Assert.Equal([columnStart, otherColumnStart], collapsed);
        Assert.Equal(0, columnStart.DurationUs);
        Assert.Equal(200, otherColumnStart.DurationUs);
    }

    private static SegmentScanEvent Scan(bool isStart, long rowGroup, int sequence, long timeUs, int column = 3, long outputRows = 0) => new()
    {
        IsScanStart = isStart,
        NodeId = 1,
        RowGroupId = rowGroup,
        ColumnId = column,
        SequenceId = sequence,
        TimeUs = timeUs,
        OutputRows = outputRows
    };
}
