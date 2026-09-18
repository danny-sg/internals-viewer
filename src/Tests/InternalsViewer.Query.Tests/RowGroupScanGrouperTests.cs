using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class RowGroupScanGrouperTests
{
    private const string Finished = "query_execution_column_store_rowgroup_scan_finished";

    private static readonly PlanNodeIdentifier Scan = new(1, 1);

    [Fact]
    public void Group_Gathers_A_Rowgroups_Events_Up_To_Its_Finish()
    {
        var lookup = new ObjectPoolEvent { RowGroupId = 2, SequenceId = 1, TaskAddress = 7 };

        var eliminated = new SegmentEliminateEvent { RowGroupId = 5, SequenceId = 2, TaskAddress = 7 };

        var readAhead = new ColumnStoreScanEvent
        {
            EventName = "column_store_rowgroup_readahead_issued",
            RowGroupId = 1,
            SequenceId = 3,
            TaskAddress = 7
        };

        var scan = new SegmentScanEvent { RowGroupId = 2, ColumnId = 2, SequenceId = 4, TaskAddress = 7, PlanNodeIdentifier = Scan };

        var bitmap = new ColumnStoreScanEvent { EventName = "column_store_expression_filter_bitmap_set", SequenceId = 5, TaskAddress = 7 };

        var filter = new ColumnstoreFilterEvent { EventName = ColumnstoreFilterEvent.BatchFilter, SequenceId = 6, TaskAddress = 7 };

        var otherTask = new ObjectPoolEvent { RowGroupId = 2, SequenceId = 7, TaskAddress = 8 };

        var finished = new ColumnStoreScanEvent { EventName = Finished, RowGroupId = 2, SequenceId = 8, TaskAddress = 7 };

        var unfinished = new SegmentScanEvent { RowGroupId = 1, SequenceId = 9, TaskAddress = 7 };

        var result = RowGroupScanGrouper.Group([lookup, eliminated, readAhead, scan, bitmap, filter, otherTask, finished, unfinished]);

        var group = Assert.IsType<RowGroupScanEvent>(result[0]);

        Assert.Equal(2, group.RowGroupId);
        Assert.Equal<EngineEvent>([lookup, scan, filter, finished], group.Events);
        Assert.Equal<EngineEvent>([group, eliminated, readAhead, bitmap, otherTask, unfinished], result);
    }

    [Fact]
    public void Group_Gives_Members_Without_An_Operator_The_Operator_Of_Their_Segment_Scans()
    {
        var elsewhere = new PlanNodeIdentifier(1, 9);

        var lookup = new ObjectPoolEvent { RowGroupId = 2, SequenceId = 1, PlanNodeIdentifier = elsewhere };

        var scan = new SegmentScanEvent { RowGroupId = 2, SequenceId = 2, PlanNodeIdentifier = Scan };

        var filter = new ColumnstoreFilterEvent { EventName = ColumnstoreFilterEvent.BatchFilter, NodeId = 0, SequenceId = 3 };

        var finished = new ColumnStoreScanEvent { EventName = Finished, RowGroupId = 2, SequenceId = 4 };

        var group = Assert.IsType<RowGroupScanEvent>(Assert.Single(RowGroupScanGrouper.Group([lookup, scan, filter, finished])));

        Assert.Same(Scan, group.PlanNodeIdentifier);
        Assert.Same(Scan, filter.PlanNodeIdentifier);
        Assert.Same(Scan, finished.PlanNodeIdentifier);
        Assert.Same(elsewhere, lookup.PlanNodeIdentifier);
    }

    [Fact]
    public void Fit_Keeps_Filters_Inside_Their_Scans_And_Everything_Inside_The_Rowgroup_Scan()
    {
        var lookup = new ObjectPoolEvent { RowGroupId = 2, SequenceId = 1, TimeUs = 100, DurationUs = 400 };

        var columnOne = new SegmentScanEvent { RowGroupId = 2, ColumnId = 1, SequenceId = 2, TimeUs = 1_000, DurationUs = 5_000 };

        var columnTwo = new SegmentScanEvent { RowGroupId = 2, ColumnId = 2, SequenceId = 3, TimeUs = 1_000, DurationUs = 5_000 };

        var early = new ColumnstoreFilterEvent { EventName = ColumnstoreFilterEvent.BatchFilter, SequenceId = 4, TimeUs = 900 };

        var apply = new ColumnstoreFilterEvent
        {
            EventName = ColumnstoreFilterEvent.ExpressionFilterApply,
            ColumnId = 2,
            RowGroupId = 2,
            SequenceId = 5,
            TimeUs = 6_750,
        };

        var finished = new ColumnStoreScanEvent { EventName = Finished, RowGroupId = 2, SequenceId = 6, TimeUs = 6_500 };

        var events = RowGroupScanGrouper.Group([lookup, columnOne, columnTwo, early, apply, finished]);

        RowGroupScanGrouper.Fit(events);

        var group = Assert.IsType<RowGroupScanEvent>(Assert.Single(events));

        Assert.Equal(1_000, early.TimeUs);
        Assert.Equal(5_000, columnOne.DurationUs);
        Assert.Equal(5_750, columnTwo.DurationUs);
        Assert.Equal(6_750, finished.TimeUs);
        Assert.Equal((100L, 6_650L), (group.TimeUs, group.DurationUs));
    }

    [Fact]
    public void Flatten_Lists_A_Groups_Members_In_Place_Of_The_Group()
    {
        var scan = new SegmentScanEvent { RowGroupId = 2, SequenceId = 1 };

        var finished = new ColumnStoreScanEvent { EventName = Finished, RowGroupId = 2, SequenceId = 2 };

        var wait = new EngineEvent { Name = "wait", SequenceId = 3 };

        var events = RowGroupScanGrouper.Group([scan, wait, finished]);

        Assert.Equal<EngineEvent>([scan, finished, wait], RowGroupScanGrouper.Flatten(events));
    }
}
