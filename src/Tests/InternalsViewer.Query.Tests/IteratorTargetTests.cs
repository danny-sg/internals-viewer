using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class IteratorTargetTests
{
    [Fact]
    public void An_Iterator_Matched_To_An_Operator_Leads_To_It()
    {
        var tree = new CallStackTree();

        var scan = tree.AddCall(tree.Root, Iterator(0x10, 0x100), 1);

        var seek = new ExecutionOperatorEvent
        {
            Name = "Index Seek",
            OperatorDescription = "Index Seek",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = 2 },
            EntryFrames = [scan]
        };

        var target = IteratorTarget.Build(tree, [seek])[0x100];

        Assert.Same(seek, target.Operator);
        Assert.Same(scan, target.Node);
        Assert.Equal("Index Seek (Node 2)", target.Label);
    }

    [Fact]
    public void An_Unmatched_Iterator_Leads_To_Its_Outermost_Frame()
    {
        var tree = new CallStackTree();

        var open = tree.AddCall(tree.Root, Iterator(0x10, 0x200), 1);

        tree.AddCall(open, Iterator(0x20, 0x200), 1);

        var target = IteratorTarget.Build(tree, [])[0x200];

        Assert.Null(target.Operator);
        Assert.Same(open, target.Node);
    }

    private static CallstackFrame Iterator(uint rva, ulong instance) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Instance = instance
    };
}
