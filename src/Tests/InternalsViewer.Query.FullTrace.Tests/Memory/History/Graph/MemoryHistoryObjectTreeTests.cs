using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Graph;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History.Graph;

[Trait("Category", "Unit")]
public class MemoryHistoryObjectTreeTests
{
    private const ulong First = 0x1000;

    private const ulong Second = 0x2000;

    private const ulong Third = 0x3000;

    [Fact]
    public void Each_Object_Hangs_Off_The_Object_That_Created_It()
    {
        var store = CreateStore();

        var tree = MemoryHistoryObjectTree.Build(store, default, TracePosition.Unknown, 10, GetCreator(store));

        Assert.Equal([(First, -1), (Second, 0), (Third, 1)], tree.Nodes.Select(n => (store.Blocks[n.Block].Address, n.Parent)));

        Assert.False(tree.IsTruncated);
    }

    [Fact]
    public void An_Object_Created_By_One_Made_Before_The_Window_Starts_At_The_Top()
    {
        var store = CreateStore();

        var tree = MemoryHistoryObjectTree.Build(store, At(2), TracePosition.Unknown, 10, GetCreator(store));

        Assert.Equal([(Second, -1), (Third, 0)], tree.Nodes.Select(n => (store.Blocks[n.Block].Address, n.Parent)));
    }

    [Fact]
    public void A_Tree_Past_Its_Limit_Stops_Growing_And_Says_So()
    {
        var store = CreateStore();

        var tree = MemoryHistoryObjectTree.Build(store, default, TracePosition.Unknown, 2, GetCreator(store));

        Assert.Equal((2, true), (tree.Nodes.Count, tree.IsTruncated));
    }

    private static MemoryHistoryStore CreateStore()
        => MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Third, 0x10, from: 3),
                                           MemoryHistoryStoreHelper.Block(First, 0x10, from: 1),
                                           MemoryHistoryStoreHelper.Block(Second, 0x10, from: 2)],
                                          [(1, First + 8, [1]), (2, Second + 8, [1]), (3, Third + 8, [1])]);

    private static Func<int, int> GetCreator(MemoryHistoryStore store)
    {
        var creators = new Dictionary<ulong, ulong> { [Second] = First, [Third] = Second };

        return block => creators.TryGetValue(store.Blocks[block].Address, out var creator)
            ? MemoryHistoryStoreHelper.GetBlock(store, creator)
            : -1;
    }
}
