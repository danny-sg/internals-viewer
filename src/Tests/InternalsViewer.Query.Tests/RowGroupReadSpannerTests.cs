using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Consolidation;

namespace InternalsViewer.Query.Tests;

public class RowGroupReadSpannerTests
{
    [Fact]
    public void Apply_Spans_A_Read_To_The_Last_Segment_Scan_End_Of_Its_Rowgroup()
    {
        var read = new ColumnStoreScanEvent { EventName = "column_store_rowgroup_read_issued", RowGroupId = 3, SequenceId = 1, TimeUs = 1000, TaskAddress = 7 };

        var first = new SegmentScanEvent { RowGroupId = 3, SequenceId = 2, TimeUs = 1500, DurationUs = 400, TaskAddress = 7 };

        var last = new SegmentScanEvent { RowGroupId = 3, SequenceId = 3, TimeUs = 1600, DurationUs = 900, TaskAddress = 7 };

        var otherRowGroup = new SegmentScanEvent { RowGroupId = 4, SequenceId = 4, TimeUs = 1700, DurationUs = 5000, TaskAddress = 7 };

        var otherTask = new SegmentScanEvent { RowGroupId = 3, SequenceId = 5, TimeUs = 1700, DurationUs = 5000, TaskAddress = 8 };

        RowGroupReadSpanner.Apply([last, otherTask, read, otherRowGroup, first]);

        Assert.Equal(1500, read.DurationUs);
    }

    [Fact]
    public void Apply_Stamps_Pool_Lookups_With_The_Rowgroup_Of_The_Next_Read_Or_Scan_On_Their_Task()
    {
        var miss = new ObjectPoolEvent { IsHit = false, SequenceId = 1, TimeUs = 900, TaskAddress = 7 };

        var otherTask = new ObjectPoolEvent { IsHit = true, SequenceId = 2, TaskAddress = 8 };

        var read = new ColumnStoreScanEvent { EventName = "column_store_rowgroup_read_issued", RowGroupId = 5, SequenceId = 3, TimeUs = 1000, TaskAddress = 7 };

        var hit = new ObjectPoolEvent { IsHit = true, SequenceId = 4, TaskAddress = 7 };

        var scan = new SegmentScanEvent { RowGroupId = 6, SequenceId = 5, TimeUs = 1100, DurationUs = 100, TaskAddress = 7 };

        RowGroupReadSpanner.Apply([scan, hit, read, otherTask, miss]);

        Assert.Equal(5, miss.RowGroupId);
        Assert.Equal(900, read.TimeUs);
        Assert.Null(otherTask.RowGroupId);
        Assert.Equal(6, hit.RowGroupId);
    }

    [Fact]
    public void Apply_Leaves_A_Read_With_No_Segment_Scans_As_An_Instant()
    {
        var read = new ColumnStoreScanEvent { EventName = "column_store_rowgroup_readahead_issued", RowGroupId = 1, SequenceId = 1, TimeUs = 1000 };

        var earlier = new SegmentScanEvent { RowGroupId = 1, SequenceId = 0, TimeUs = 200, DurationUs = 300 };

        RowGroupReadSpanner.Apply([earlier, read]);

        Assert.Equal(0, read.DurationUs);
    }
}
