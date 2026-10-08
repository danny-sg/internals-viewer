using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelCallMergerTests
{
    private const ulong SqlMinBase = 0x7FF8_0000_0000;

    private const int ActivityBuckets = 4;

    [Fact]
    public void Merge_Adds_Each_Call_Under_Its_Caller()
    {
        var tree = new CallStackTree();

        var calls = new TimeTravelCallTree([new TimeTravelCallNode(-1, 0, SqlMinBase + 0x100, 0, 1),
                                            new TimeTravelCallNode(0, 0, SqlMinBase + 0x200, 0, 12)],
                                           [],
                                           [new TimeTravelModule(@"Z:\Missing\sqlmin.dll", SqlMinBase, 0x10000)]);

        TimeTravelCallMerger.Merge(tree, calls, ActivityBuckets);

        var root = Assert.Single(tree.Root.ChildNodes);
        var child = Assert.Single(root.ChildNodes);

        Assert.Equal("sqlmin", root.Frame?.Module);
        Assert.Equal(0x100u, root.Frame?.Rva);
        Assert.Equal(1, root.Calls);
        Assert.Equal(0x200u, child.Frame?.Rva);
        Assert.Equal(12, child.Calls);
    }

    [Fact]
    public void Merge_Keeps_Calls_On_Different_Instances_Apart()
    {
        var tree = new CallStackTree();

        var calls = new TimeTravelCallTree([new TimeTravelCallNode(-1, 0, SqlMinBase + 0x100, 0, 1),
                                            new TimeTravelCallNode(0, 0, SqlMinBase + 0x200, 0xA0, 3),
                                            new TimeTravelCallNode(0, 0, SqlMinBase + 0x200, 0xB0, 4)],
                                           [],
                                           [new TimeTravelModule(@"Z:\Missing\sqlmin.dll", SqlMinBase, 0x10000)]);

        TimeTravelCallMerger.Merge(tree, calls, ActivityBuckets);

        var children = Assert.Single(tree.Root.ChildNodes).ChildNodes.OrderBy(c => c.Order).ToList();

        Assert.Equal([0xA0ul, 0xB0ul], children.Select(c => c.Frame!.Instance));
        Assert.Equal([3L, 4L], children.Select(c => c.Calls));
    }

    [Fact]
    public void Merge_Marks_An_Address_Outside_Every_Module_As_Unknown()
    {
        var tree = new CallStackTree();

        var calls = new TimeTravelCallTree([new TimeTravelCallNode(-1, 0, 0x1234, 0, 1)],
                                           [],
                                           [new TimeTravelModule(@"Z:\Missing\sqlmin.dll", SqlMinBase, 0x10000)]);

        TimeTravelCallMerger.Merge(tree, calls, ActivityBuckets);

        var node = Assert.Single(tree.Root.ChildNodes);

        Assert.Equal("Unknown@0x1234", node.Frame?.Module);
        Assert.Equal(0x1234ul, node.Frame?.Address);
    }

    [Fact]
    public void Merge_Spreads_Call_Activity_Over_The_Slices_The_Calls_Span()
    {
        var tree = new CallStackTree();

        var calls = new TimeTravelCallTree([new TimeTravelCallNode(-1, 0, SqlMinBase + 0x100, 0, 6),
                                            new TimeTravelCallNode(0, 0, SqlMinBase + 0x200, 0, 3)],
                                           [new TimeTravelCallActivity(0, 100, 2),
                                            new TimeTravelCallActivity(0, 107, 4),
                                            new TimeTravelCallActivity(1, 103, 3)],
                                           [new TimeTravelModule(@"Z:\Missing\sqlmin.dll", SqlMinBase, 0x10000)]);

        TimeTravelCallMerger.Merge(tree, calls, ActivityBuckets);

        var root = Assert.Single(tree.Root.ChildNodes);
        var child = Assert.Single(root.ChildNodes);

        Assert.True(tree.ActivityFromTrace);
        Assert.Equal([2, 0, 0, 4], root.CallActivity);
        Assert.Equal([0, 3, 0, 0], child.CallActivity);
    }
}
