using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class BatchInfoBuilderTests
{
    [Fact]
    public void Push_Down_Statistics_For_Each_Row_Group_Are_Summed_Onto_The_Scan()
    {
        var scan = new PlanNode { NodeId = 1 };

        var hashMatch = new PlanNode { NodeId = 0, Children = [scan] };

        var plan = new ExecutionPlan(1) { Root = [hashMatch] };

        EngineEvent[] events =
        [
            Statistics(rowGroup: 1, pushed: 1000, overflow: 0),
            Statistics(rowGroup: 0, pushed: 0, overflow: 500)
        ];

        BatchInfoBuilder.Apply(events, [plan]);

        Assert.Equal(1000, scan.BatchInfo!.RowsPushedDown);
        Assert.Equal(500, scan.BatchInfo.RowsNotPushedOverflow);
        Assert.Equal(0, scan.BatchInfo.RowsNotPushedEncoding);
        Assert.Null(hashMatch.BatchInfo);
    }

    [Fact]
    public void A_Compile_Time_Push_Down_Event_Adds_No_Row_Counts()
    {
        var hashMatch = new PlanNode { NodeId = 0 };

        var plan = new ExecutionPlan(1) { Root = [hashMatch] };

        var compiled = new BatchModeEvent
        {
            EventName = "query_execution_push_down_aggregate",
            NodeId = 0,
            AggregationCount = 2,
            GroupByCount = 0
        };

        BatchInfoBuilder.Apply([compiled], [plan]);

        Assert.Null(hashMatch.BatchInfo!.RowsPushedDown);
        Assert.Null(hashMatch.BatchInfo.RowsNotPushedOverflow);
    }

    private static BatchModeEvent Statistics(long rowGroup, long pushed, long overflow) => new()
    {
        EventName = "query_execution_dynamic_push_down_statistics",
        NodeId = 1,
        RowGroupId = rowGroup,
        RowsPushedDown = pushed,
        RowsNotPushedEncoding = 0,
        RowsNotPushedOverflow = overflow,
        RowsNotPushedDisabled = 0
    };
}
