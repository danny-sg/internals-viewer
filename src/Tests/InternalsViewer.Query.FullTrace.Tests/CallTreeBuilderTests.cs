using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class CallTreeBuilderTests
{
    private const ulong SqlMinBase = 0x7FF8_0000_0000;

    private static readonly TraceModule[] Modules = [TraceModule.Read(@"Z:\Missing\sqlmin.dll", SqlMinBase, 0x10000)];

    [Fact]
    public void Build_Adds_Each_Call_Under_Its_Caller()
    {
        CallNode[] calls = [new CallNode(-1, 0, SqlMinBase + 0x100, 0, 1),
                            new CallNode(0, 0, SqlMinBase + 0x200, 0, 12)];

        var (tree, _) = CallTreeBuilder.Build(calls, Modules);

        var root = Assert.Single(tree.Root.ChildNodes);
        var child = Assert.Single(root.ChildNodes);

        Assert.Equal("sqlmin", root.Frame?.Module);
        Assert.Equal(0x100u, root.Frame?.Rva);
        Assert.Equal(1, root.Calls);
        Assert.Equal(0x200u, child.Frame?.Rva);
        Assert.Equal(12, child.Calls);
    }

    [Fact]
    public void Build_Keeps_Calls_On_Different_Instances_Apart()
    {
        CallNode[] calls = [new CallNode(-1, 0, SqlMinBase + 0x100, 0, 1),
                            new CallNode(0, 0, SqlMinBase + 0x200, 0xA0, 3),
                            new CallNode(0, 0, SqlMinBase + 0x200, 0xB0, 4)];

        var (tree, _) = CallTreeBuilder.Build(calls, Modules);

        var children = Assert.Single(tree.Root.ChildNodes).ChildNodes.OrderBy(c => c.Order).ToList();

        Assert.Equal([0xA0ul, 0xB0ul], children.Select(c => c.Frame!.InstanceAddress));
        Assert.Equal([3L, 4L], children.Select(c => c.Calls));
    }

    [Fact]
    public void Build_Marks_An_Address_Outside_Every_Module_As_Unknown()
    {
        CallNode[] calls = [new CallNode(-1, 0, 0x1234, 0, 1)];

        var (tree, _) = CallTreeBuilder.Build(calls, Modules);

        var node = Assert.Single(tree.Root.ChildNodes);

        Assert.Equal("Unknown@0x1234", node.Frame?.Module);
        Assert.Equal(0x1234ul, node.Frame?.Address);
    }

    [Fact]
    public void Build_Marks_An_Address_Past_The_End_Of_A_Module_As_Unknown()
    {
        CallNode[] calls = [new CallNode(-1, 0, SqlMinBase + 0x10000, 0, 1)];

        var (tree, _) = CallTreeBuilder.Build(calls, Modules);

        var node = Assert.Single(tree.Root.ChildNodes);

        Assert.Equal($"Unknown@0x{SqlMinBase + 0x10000:X}", node.Frame?.Module);
    }
}
