using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;

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
                                           [new TimeTravelModule(@"Z:\Missing\sqlmin.dll", SqlMinBase, 0x10000)]);

        TimeTravelCallMerger.Merge(tree, calls);

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
                                           [new TimeTravelModule(@"Z:\Missing\sqlmin.dll", SqlMinBase, 0x10000)]);

        TimeTravelCallMerger.Merge(tree, calls);

        var children = Assert.Single(tree.Root.ChildNodes).ChildNodes.OrderBy(c => c.Order).ToList();

        Assert.Equal([0xA0ul, 0xB0ul], children.Select(c => c.Frame!.Instance));
        Assert.Equal([3L, 4L], children.Select(c => c.Calls));
    }

    [Fact]
    public void Merge_Marks_An_Address_Outside_Every_Module_As_Unknown()
    {
        var tree = new CallStackTree();

        var calls = new TimeTravelCallTree([new TimeTravelCallNode(-1, 0, 0x1234, 0, 1)],
                                           [new TimeTravelModule(@"Z:\Missing\sqlmin.dll", SqlMinBase, 0x10000)]);

        TimeTravelCallMerger.Merge(tree, calls);

        var node = Assert.Single(tree.Root.ChildNodes);

        Assert.Equal("Unknown@0x1234", node.Frame?.Module);
        Assert.Equal(0x1234ul, node.Frame?.Address);
    }

    [Fact]
    public void Activity_Counts_Each_Call_In_The_Bucket_Where_It_Starts()
    {
        var tree = new CallStackTree();

        var root = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x100 }, 3);
        var child = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x200 }, 2);

        var builder = new TimeTravelTimeline.Builder();

        builder.Add([Span(0, 0, 10), Span(0, 80, 85), Span(0, 90, 100), Span(1, 30, 35), Span(1, 31, 32, unknownStart: true)]);

        var timeline = builder.Build([new(-1, 0, 0x100, 0, 3), new(0, 0, 0x200, 0, 2)]);

        timeline.MapNodes([root, child]);

        TimeTravelCallMerger.AddActivity(timeline, ActivityBuckets);

        Assert.Equal([1, 0, 0, 2], root.CallActivity);
        Assert.Equal([0, 1, 0, 0], child.CallActivity);
    }

    private static TimeTravelCallSpan Span(int node, ulong start, ulong end, bool unknownStart = false)
        => new(start, 0, end, 0, start, end, node, 7, uint.MaxValue, unknownStart ? 3u : 1u);
}
