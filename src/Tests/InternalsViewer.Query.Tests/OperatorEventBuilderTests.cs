using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

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
