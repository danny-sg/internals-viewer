using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests.UnitTests.Events.Operators;

[Trait("Category", "Unit")]
public class OperatorEventBuilderTests
{
    [Fact]
    public void Inputs_A_Parent_Consumes_Before_Returning_Rows_Are_Blocking_Inputs()
    {
        var build = Node(2, "Index Scan", "Index Scan", estimatedRows: 10);

        var probe = Node(3, "Index Scan", "Index Scan", estimatedRows: 1_000);

        var hash = Node(1, "Hash Match", "Inner Join", children: [build, probe]);

        var sort = Node(0, "Sort", "Sort", children: [hash]);

        var plan = new ExecutionPlan(1) { Root = [sort], NodesById = new() { [0] = sort, [1] = hash, [2] = build, [3] = probe } };

        var events = new OperatorEventBuilder(plan, []).Build().ToDictionary(e => e.PlanNodeIdentifier!.NodeId);

        Assert.Equal((false, true, true, false),
                     (events[0].IsBlockingInput, events[1].IsBlockingInput, events[2].IsBlockingInput, events[3].IsBlockingInput));
    }

    [Fact]
    public void The_First_Input_Of_A_Hash_Match_Is_Its_Build_Input_Whatever_Its_Estimate()
    {
        var build = Node(1, "Index Scan", "Index Scan", estimatedRows: 1_000);

        var probe = Node(2, "Index Scan", "Index Scan", estimatedRows: 10);

        var hash = Node(0, "Hash Match", "Inner Join", children: [build, probe]);

        var plan = new ExecutionPlan(1) { Root = [hash], NodesById = new() { [0] = hash, [1] = build, [2] = probe } };

        var events = new OperatorEventBuilder(plan, []).Build().ToDictionary(e => e.PlanNodeIdentifier!.NodeId);

        Assert.Equal((true, false), (events[1].IsBlockingInput, events[2].IsBlockingInput));
    }

    private static PlanNode Node(int nodeId,
                                 string physicalOperator,
                                 string logicalOperator,
                                 long estimatedRows = 1,
                                 List<PlanNode>? children = null) => new()
    {
        NodeId = nodeId,
        PhysicalOperator = physicalOperator,
        LogicalOperator = logicalOperator,
        EstimatedRows = estimatedRows,
        Children = children ?? []
    };
}
