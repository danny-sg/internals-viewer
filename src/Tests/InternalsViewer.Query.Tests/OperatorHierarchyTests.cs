using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class OperatorHierarchyTests
{
    [Fact]
    public void Descendants_Are_The_Whole_Subtree_Not_Just_The_Children()
    {
        var hierarchy = OperatorHierarchy.Build([Operator(0, "Nested Loops"),
                                                 Operator(1, "Hash Match", parent: 0),
                                                 Operator(2, "Index Scan", parent: 1),
                                                 Operator(3, "Index Seek", parent: 0)]);

        var join = hierarchy.Operators[0];

        Assert.Equal([1, 2, 3], hierarchy.Descendants(join).Select(o => o.PlanNodeIdentifier!.NodeId).Order());
        Assert.Equal([1, 3], hierarchy.Children(join).Select(o => o.PlanNodeIdentifier!.NodeId).Order());
    }

    [Fact]
    public void Roots_Are_The_Operators_Nothing_Captured_Is_The_Parent_Of()
    {
        // The statement node usually, but a plan whose statement was not built has its top operators as roots instead —
        // an operator naming a parent that is not here cannot be reached from anything and would otherwise be lost.
        var hierarchy = OperatorHierarchy.Build([Operator(1, "Hash Match", parent: 99),
                                                 Operator(2, "Index Scan", parent: 1),
                                                 Operator(3, "Index Seek")]);

        Assert.Equal([1, 3], hierarchy.Roots.Select(o => o.PlanNodeIdentifier!.NodeId));
    }

    [Fact]
    public void Parent_Is_Null_When_The_Plan_Named_One_That_Was_Not_Captured()
    {
        // A parent id that resolves to nothing is not the same as no parent, but for walking up it has to behave like
        // one — the alternative is the link back out pointing at an operator that does not exist.
        var hierarchy = OperatorHierarchy.Build([Operator(1, "Hash Match", parent: 99),
                                                 Operator(2, "Index Scan", parent: 1),
                                                 Operator(3, "Index Seek")]);

        Assert.Equal(1, hierarchy.Parent(hierarchy.Operators[1])?.PlanNodeIdentifier!.NodeId);
        Assert.Null(hierarchy.Parent(hierarchy.Operators[0]));
        Assert.Null(hierarchy.Parent(hierarchy.Operators[2]));
    }

    [Fact]
    public void A_Cycle_In_The_Plan_Does_Not_Hang_The_Walk()
    {
        // Nothing enforces that the parent ids describe a tree, and a hang here would take the whole run with it.
        var hierarchy = OperatorHierarchy.Build([Operator(0, "Nested Loops", parent: 1),
                                                 Operator(1, "Hash Match", parent: 0)]);

        Assert.Equal([1], hierarchy.Descendants(hierarchy.Operators[0]).Select(o => o.PlanNodeIdentifier!.NodeId));
    }

    [Fact]
    public void A_Scope_Is_The_Subtrees_Events_Expanded_Through_What_They_Own()
    {
        // The aggregate emits nothing itself — only the scan's reads exist — so scoping it to its own events would
        // leave it blank. And the group carries the node id while its members carry the frames, so it must expand.
        var member = new IoEvent { Name = "read" };

        var group = new ReadEventGroup { Name = "Page Read", Events = [member], PlanNodeIdentifier = NodeId(2) };

        var elsewhere = new IoEvent { Name = "read", PlanNodeIdentifier = NodeId(9) };

        var operators = new List<EngineEvent> { Operator(1, "Stream Aggregate"), Operator(2, "Index Scan", parent: 1) };

        var hierarchy = OperatorHierarchy.Build(operators);

        var scope = hierarchy.ScopeOf(hierarchy.Operators[0], [.. operators, group, elsewhere]);

        Assert.Contains(group, scope);
        Assert.Contains(member, scope);
        Assert.DoesNotContain(elsewhere, scope);
    }

    private static PlanNodeIdentifier NodeId(int nodeId) => new() { PlanHandleId = 1, NodeId = nodeId };

    private static ExecutionOperatorEvent Operator(int node, string physicalOperator, int? parent = null) => new()
    {
        Name = physicalOperator,
        OperatorDescription = physicalOperator,
        PlanNodeIdentifier = NodeId(node),
        ParentNodeId = parent,
    };
}
