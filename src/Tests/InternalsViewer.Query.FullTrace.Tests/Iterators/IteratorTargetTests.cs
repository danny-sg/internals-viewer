using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators;

[Trait("Category", "Unit")]
public class IteratorTargetTests
{
    [Fact]
    public void An_Iterator_Matched_To_An_Operator_Leads_To_It()
    {
        var tree = new CallTree();

        var scan = tree.AddCall(tree.Root, Iterator(0x10, 0x100), 1);

        var seek = new ExecutionOperatorEvent
        {
            Name = "Index Seek",
            OperatorDescription = "Index Seek",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = 2 }
        };

        var frames = new OperatorFrameMap();

        frames.SetEntryFrames(seek, [scan]);

        var link = new IteratorPlanLink(new Dictionary<ulong, PlanNodeIdentifier>(),
                                        new Dictionary<ulong, PlanNodeIdentifier> { [0x100] = new(1, 2) });

        var target = IteratorTarget.Build(tree, [seek], link, frames)[0x100];

        Assert.Same(seek, target.Operator);
        Assert.Same(scan, target.Node);
        Assert.Equal("Index Seek (Node 2)", target.Label);
    }

    [Fact]
    public void An_Unmatched_Iterator_Leads_To_Its_Outermost_Frame()
    {
        var tree = new CallTree();

        var open = tree.AddCall(tree.Root, Iterator(0x10, 0x200), 1);

        tree.AddCall(open, Iterator(0x20, 0x200), 1);

        var target = IteratorTarget.Build(tree, [], IteratorPlanLink.Empty, OperatorFrameMap.Empty)[0x200];

        Assert.Null(target.Operator);
        Assert.Same(open, target.Node);
    }

    [Fact]
    public void A_Plan_Object_Leads_To_Its_Operator_Even_Without_A_Frame_Of_Its_Own()
    {
        var tree = new CallTree();

        var scan = tree.AddCall(tree.Root, Iterator(0x10, 0x100), 1);

        var seek = new ExecutionOperatorEvent
        {
            Name = "Index Seek",
            OperatorDescription = "Index Seek",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = 2 }
        };

        var frames = new OperatorFrameMap();

        frames.SetEntryFrames(seek, [scan]);

        var link = new IteratorPlanLink(new Dictionary<ulong, PlanNodeIdentifier> { [0xA0] = new(1, 2) },
                                        new Dictionary<ulong, PlanNodeIdentifier>());

        var target = IteratorTarget.Build(tree, [seek], link, frames)[0xA0];

        Assert.Equal(IteratorTargetType.PlanObject, target.Type);
        Assert.Same(seek, target.Operator);
        Assert.Same(scan, target.Node);
    }

    [Fact]
    public void A_Profiling_Wrapper_Is_A_Profiling_Iterator_And_The_Wrapped_Iterator_Is_Not()
    {
        var tree = new CallTree();

        var wrapper = tree.AddCall(tree.Root, Method("CQScanProfileNew", 0x10, 0x300), 1);

        tree.AddCall(wrapper, Method("CQScanTopNew", 0x20, 0x400), 1);

        var top = new ExecutionOperatorEvent
        {
            Name = "Top",
            OperatorDescription = "Top",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = 0 }
        };

        var link = new IteratorPlanLink(new Dictionary<ulong, PlanNodeIdentifier>(),
                                        new Dictionary<ulong, PlanNodeIdentifier> { [0x300] = new(1, 0), [0x400] = new(1, 0) });

        var targets = IteratorTarget.Build(tree, [top], link, OperatorFrameMap.Empty);

        Assert.Equal((IteratorTargetType.ProfilingIterator, IteratorTargetType.Iterator), (targets[0x300].Type, targets[0x400].Type));
    }

    private static CallFrame Method(string className, uint rva, ulong instance) => new()
    {
        Module = new("sqlmin"),
        Rva = rva,
        InstanceAddress = instance,
        Function = new FrameFunction { ClassName = className, MethodName = "GetRow" }
    };

    private static CallFrame Iterator(uint rva, ulong instance) => new()
    {
        Module = new("sqlmin"),
        Rva = rva,
        InstanceAddress = instance
    };
}
