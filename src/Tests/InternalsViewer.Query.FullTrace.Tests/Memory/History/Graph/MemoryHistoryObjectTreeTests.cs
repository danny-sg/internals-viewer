using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Graph;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History.Graph;

[Trait("Category", "Unit")]
public class MemoryHistoryObjectTreeTests
{
    private const ulong Context = 0x500;

    private const ulong First = 0x1000;

    private const ulong Second = 0x2000;

    private const ulong Third = 0x3000;

    private const ulong Fourth = 0x4000;

    [Fact]
    public void Each_Object_Hangs_Under_The_Objects_On_The_Stack_That_Created_It()
    {
        var store = CreateStore();

        var tree = MemoryHistoryObjectTree.Build(store, default, TracePosition.Unknown, 10, GetPath(store));

        Assert.Equal([("Source", 0ul, -1), ("ExecContext", Context, 0), ("", First, 1), ("", Second, 2), ("", Third, 3),
                      ("Second", Second, 0), ("", Fourth, 5)],
                     tree.Nodes.Select(n => (n.ClassName, n.Instance, n.Parent)));

        Assert.False(tree.IsTruncated);
    }

    [Fact]
    public void A_Recorded_Object_Is_The_Same_Node_When_Later_Calls_Reach_It_The_Same_Way()
    {
        var store = CreateStore();

        var tree = MemoryHistoryObjectTree.Build(store, default, TracePosition.Unknown, 10, GetPath(store));

        var third = tree.Nodes.Single(n => n.Instance == Third);

        Assert.Equal(Second, tree.Nodes[third.Parent].Instance);

        Assert.True(tree.Nodes[third.Parent].IsBlock);
    }

    [Fact]
    public void An_Object_Reached_Through_Other_Calls_Is_Listed_Again_Under_Them()
    {
        var store = CreateStore();

        var tree = MemoryHistoryObjectTree.Build(store, default, TracePosition.Unknown, 10, GetPath(store));

        var fourth = tree.Nodes.Single(n => n.Instance == Fourth);

        Assert.Equal(("Second", Second, 0), (tree.Nodes[fourth.Parent].ClassName, tree.Nodes[fourth.Parent].Instance,
                                             tree.Nodes[fourth.Parent].Parent));

        Assert.Equal(2, tree.Nodes.Count(n => n.Instance == Second));
    }

    [Fact]
    public void An_Object_Made_Before_The_Window_Is_Listed_By_Its_Stack_Only()
    {
        var store = CreateStore();

        var tree = MemoryHistoryObjectTree.Build(store, At(2), TracePosition.Unknown, 10, GetPath(store));

        Assert.Equal([("Source", 0ul, -1), ("ExecContext", Context, 0), ("First", First, 1), ("", Second, 2), ("", Third, 3),
                      ("Second", Second, 0), ("", Fourth, 5)],
                     tree.Nodes.Select(n => (n.ClassName, n.Instance, n.Parent)));
    }

    [Fact]
    public void A_Tree_Past_Its_Limit_Stops_Growing_And_Says_So()
    {
        var store = CreateStore();

        var tree = MemoryHistoryObjectTree.Build(store, default, TracePosition.Unknown, 3, GetPath(store));

        Assert.Equal((3, true), (tree.Nodes.Count(n => n.IsBlock), tree.IsTruncated));
    }

    private static MemoryHistoryStore CreateStore()
        => MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Third, 0x10, from: 3),
                                           MemoryHistoryStoreHelper.Block(First, 0x10, from: 1),
                                           MemoryHistoryStoreHelper.Block(Second, 0x10, from: 2),
                                           MemoryHistoryStoreHelper.Block(Fourth, 0x10, from: 4)],
                                          [(1, First + 8, [1]), (2, Second + 8, [1]), (3, Third + 8, [1]), (4, Fourth + 8, [1])]);

    private static Func<int, IReadOnlyList<MemoryHistoryCallObject>> GetPath(MemoryHistoryStore store)
    {
        MemoryHistoryCallObject[] outer = [new("Source", 0), new("ExecContext", Context)];

        var paths = new Dictionary<ulong, MemoryHistoryCallObject[]>
        {
            [First] = outer,
            [Second] = [.. outer, new("First", First)],
            [Third] = [.. outer, new("First", First), new("Second", Second)],
            [Fourth] = [new("Source", 0), new("Second", Second)]
        };

        return block => paths[store.Blocks[block].Address];
    }
}
