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

        Assert.Equal((InputRole.Streaming, InputRole.Blocking, InputRole.Blocking, InputRole.Matched),
                     (events[0].InputRole, events[1].InputRole, events[2].InputRole, events[3].InputRole));
    }

    [Fact]
    public void Only_Inputs_A_Parent_Can_Drop_Rows_From_Are_Filtered_Or_Matched()
    {
        var outer = Node(3, "Index Scan", "Index Scan");

        var inner = Node(4, "Index Seek", "Index Seek");

        var loop = Node(2, "Nested Loops", "Inner Join", children: [outer, inner]);

        var aggregate = Node(1, "Stream Aggregate", "Aggregate", children: [loop]);

        var filter = Node(0, "Filter", "Filter", children: [aggregate]);

        var plan = new ExecutionPlan(1)
        {
            Root = [filter],
            NodesById = new() { [0] = filter, [1] = aggregate, [2] = loop, [3] = outer, [4] = inner }
        };

        var events = new OperatorEventBuilder(plan, []).Build().ToDictionary(e => e.PlanNodeIdentifier!.NodeId);

        Assert.Equal((InputRole.Filtered, InputRole.Streaming, InputRole.Matched, InputRole.Matched),
                     (events[1].InputRole, events[2].InputRole, events[3].InputRole, events[4].InputRole));
    }

    [Fact]
    public void The_Input_Of_A_Compute_Scalar_Takes_The_Role_The_Compute_Scalar_Has()
    {
        var scan = Node(2, "Index Scan", "Index Scan");

        var computeScalar = Node(1, "Compute Scalar", "Compute Scalar", children: [scan]);

        var filter = Node(0, "Filter", "Filter", children: [computeScalar]);

        var plan = new ExecutionPlan(1) { Root = [filter], NodesById = new() { [0] = filter, [1] = computeScalar, [2] = scan } };

        var events = new OperatorEventBuilder(plan, []).Build().ToDictionary(e => e.PlanNodeIdentifier!.NodeId);

        Assert.Equal((InputRole.Filtered, InputRole.Filtered), (events[1].InputRole, events[2].InputRole));
    }

    [Fact]
    public void The_First_Input_Of_A_Hash_Match_Is_Its_Build_Input_Whatever_Its_Estimate()
    {
        var build = Node(1, "Index Scan", "Index Scan", estimatedRows: 1_000);

        var probe = Node(2, "Index Scan", "Index Scan", estimatedRows: 10);

        var hash = Node(0, "Hash Match", "Inner Join", children: [build, probe]);

        var plan = new ExecutionPlan(1) { Root = [hash], NodesById = new() { [0] = hash, [1] = build, [2] = probe } };

        var events = new OperatorEventBuilder(plan, []).Build().ToDictionary(e => e.PlanNodeIdentifier!.NodeId);

        Assert.Equal((InputRole.Blocking, InputRole.Matched), (events[1].InputRole, events[2].InputRole));
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
