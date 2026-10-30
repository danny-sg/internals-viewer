using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MemoryHistoryGraphTests
{
    private const ulong First = 0x1000;

    private const ulong Second = 0x2000;

    private const ulong Third = 0x3000;

    private const ulong Fourth = 0x4000;

    private const ulong Fifth = 0x5000;

    private const ulong Frame = 0x20A_5448_0000;

    [Fact]
    public void A_Pointer_Back_To_An_Object_Already_In_The_Graph_Joins_It_Instead_Of_Adding_It_Again()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10), MemoryHistoryStores.Block(Second, 0x10)],
                                              [(2, First, Pointer(Second)), (3, Second + 8, Pointer(First))]);

        var first = MemoryHistoryStores.GetBlock(store, First);

        var graph = Build(store, TraceBufferPool.Empty, first, 10);

        Assert.Equal([first, MemoryHistoryStores.GetBlock(store, Second)], graph.Nodes.Select(n => n.Targets.Single().Block));

        Assert.Equal([new MemoryHistoryGraphEdge(0, 1, 0, 0, 0), new(1, 0, 8, 0, 0)], graph.Edges);
    }

    [Fact]
    public void A_Pointer_To_Another_Offset_Of_An_Object_Already_In_The_Graph_Links_To_Its_Box()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10), MemoryHistoryStores.Block(Second, 0x10)],
                                              [(2, First, Pointer(Second)), (3, Second + 8, Pointer(First + 8))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10);

        Assert.Equal((2, true), (graph.Nodes.Count, graph.Edges.Contains(new MemoryHistoryGraphEdge(1, 0, 8, 8, 0))));
    }

    [Fact]
    public void Fields_Pointing_At_Different_Offsets_Of_One_Object_Share_Its_Box()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10), MemoryHistoryStores.Block(Second, 0x10)],
                                              [Touch(Second), (2, First, Pointer(Second)), (3, First + 8, Pointer(Second + 8))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10);

        Assert.Equal([new MemoryHistoryGraphEdge(0, 1, 0, 0, 0), new(0, 1, 8, 8, 0)], graph.Edges);

        Assert.All(graph.Edges, e => Assert.True(graph.IsTreeEdge(e)));
    }

    [Fact]
    public void A_Pointer_To_Objects_Already_In_The_Graph_Links_To_Their_Box()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10),
                                               MemoryHistoryStores.Block(Fourth, 0x10)],
                                              [Touch(Second),
                                               Touch(Third),
                                               (2, First, Pointer(Second)),
                                               (4, First, Pointer(Third)),
                                               (2, First + 8, Pointer(Fourth)),
                                               (3, Fourth, Pointer(Third))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10);

        Assert.Equal((3, true), (graph.Nodes.Count, graph.Edges.Contains(new MemoryHistoryGraphEdge(2, 1, 0, 0, 0))));
    }

    [Fact]
    public void Pointers_Into_The_Same_Page_Share_One_Box()
    {
        var builder = new TraceBufferPool.Builder();

        builder.Add(new TraceBufferPage(Frame, new PageAddress(1, 100), 6, At(1)));

        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10)],
                                              [(2, First, Pointer(Frame + 0x60)), (3, First + 8, Pointer(Frame))]);

        var first = MemoryHistoryStores.GetBlock(store, First);

        var graph = Build(store, builder.Build(), first, 10);

        var page = graph.Nodes[1];

        Assert.Equal((2, new PageAddress(1, 100), true), (graph.Nodes.Count, page.Targets[0].Page?.Page, page.IsData));

        Assert.Equal([new MemoryHistoryGraphEdge(0, 1, 0, 0x60, 0), new(0, 1, 8, 0, 0)], graph.Edges);
    }

    [Fact]
    public void A_Buffer_Whose_Frame_Was_Not_Written_Points_At_The_Frame_It_Holds()
    {
        const ulong buffer = 0x7000;

        var builder = new TraceBufferPool.Builder();

        builder.Add(new TraceBufferPage(Frame, new PageAddress(1, 100), 6, At(1), buffer));

        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10), MemoryHistoryStores.Block(buffer, 0xC0)],
                                              [(2, buffer + 0x70, [1]), (3, First, Pointer(buffer))]);

        var graph = Build(store, builder.Build(), MemoryHistoryStores.GetBlock(store, First), 10);

        Assert.Equal(new PageAddress(1, 100), graph.Nodes[2].Targets.Single().Page?.Page);
    }

    [Fact]
    public void Objects_Beyond_The_Limit_Are_Left_Out_And_Marked()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10), MemoryHistoryStores.Block(Second, 0x10)],
                                              [(2, First, Pointer(Second)), (3, Second, [1])]);

        var first = MemoryHistoryStores.GetBlock(store, First);

        var graph = Build(store, TraceBufferPool.Empty, first, 1);

        Assert.Equal((1, true), (graph.Nodes.Count, graph.IsTruncated));
    }

    [Fact]
    public void A_Slot_Keeps_Its_Node_While_It_Is_Empty_And_Resolves_Its_Target_At_Each_Position()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10)],
                                              [Touch(Second),
                                               Touch(Third),
                                               (3, First, Pointer(Second)),
                                               (5, First, Pointer(0)),
                                               (7, First, Pointer(Third))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10);

        var node = graph.Nodes[1];

        Assert.Equal([-1, MemoryHistoryStores.GetBlock(store, Second), -1, MemoryHistoryStores.GetBlock(store, Third)],
                     new ulong[] { 2, 4, 6, 8 }.Select(s => node.GetTarget(At(s))?.Block ?? -1));

        Assert.Equal(1, graph.Edges.Single().ChangeCount);
    }

    [Fact]
    public void Only_Moving_To_Another_Object_After_The_Setup_Call_Counts_As_A_Change()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10)],
                                              [Touch(Second),
                                               Touch(Third),
                                               (3, First, Pointer(Third)),
                                               (4, First, Pointer(Second)),
                                               (12, First, Pointer(0)),
                                               (14, First, Pointer(Second)),
                                               (16, First, Pointer(Third)),
                                               (18, First, Pointer(Second)),
                                               (32, First, Pointer(0))],
                                              MemoryHistoryStores.Calls((2, 6), (10, 20), (30, 40)));

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10);

        Assert.Equal(2, graph.Edges.Single().ChangeCount);
    }

    [Fact]
    public void A_Move_Made_While_The_Path_Shows_Another_Object_Is_Not_Counted()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10),
                                               MemoryHistoryStores.Block(Fourth, 0x10),
                                               MemoryHistoryStores.Block(Fifth, 0x10)],
                                              [Touch(Fourth),
                                               Touch(Fifth),
                                               (2, First, Pointer(Second)),
                                               (2, Second, Pointer(Fourth)),
                                               (2, Third, Pointer(Fourth)),
                                               (4, First, Pointer(Third)),
                                               (6, Second, Pointer(Fifth))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10);

        Assert.Equal([1, 0], graph.Edges.Select(e => e.ChangeCount));
    }

    [Fact]
    public void A_Box_Is_Marked_With_The_Data_Changes_Of_Its_Object()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10), MemoryHistoryStores.Block(Third, 0x10)],
                                              [(2, First, Pointer(Third)),
                                               (2, Third, Pointer(5)),
                                               (3, Third, Pointer(6)),
                                               (5, First, Pointer(0)),
                                               (7, Third, Pointer(8))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10);

        Assert.Equal([new MemoryHistoryGraphMark(At(3), -1, false), new(At(7), -1, false)], graph.Nodes[1].Marks);
    }

    [Fact]
    public void Changes_From_The_Start_Of_Shutdown_Are_Not_Counted()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10)],
                                              [Touch(Second),
                                               Touch(Third),
                                               (2, First, Pointer(Second)),
                                               (4, First, Pointer(Third)),
                                               (12, First, Pointer(Second))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10, At(10));

        Assert.Equal(1, graph.Edges.Single().ChangeCount);
    }

    [Fact]
    public void A_Pointer_Back_Into_Its_Own_Object_Is_Not_A_Reference()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(Second, 0x10), MemoryHistoryStores.Block(Third, 0x10)],
                                              [Touch(Third),
                                               (2, Second, Pointer(Third)),
                                               (3, Second, Pointer(Second + 8)),
                                               (4, Second, Pointer(Third))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, Second), 10);

        Assert.Equal((2, 0), (graph.Nodes.Count, graph.Edges.Single().ChangeCount));
    }

    [Fact]
    public void A_Box_Holding_One_Object_Shows_It_While_It_Exists_Before_Anything_Points_At_It()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10), MemoryHistoryStores.Block(Second, 0x10, from: 2)],
                                              [(3, Second + 8, [1]), (5, First, Pointer(Second))]);

        var node = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10).Nodes[1];

        Assert.Equal((false, Second, Second),
                     (node.GetTarget(At(1)) is not null, node.GetTarget(At(3))?.Value, node.GetTarget(At(6))?.Value));
    }

    [Fact]
    public void A_Pointer_Is_Empty_Once_Its_Target_Is_Freed()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10), MemoryHistoryStores.Block(Second, 0x10, to: 4)],
                                              [Touch(Second), (2, First, Pointer(Second))]);

        var node = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10).Nodes[1];

        Assert.Equal((true, false), (node.GetTarget(At(3)) is not null, node.GetTarget(At(5)) is not null));
    }

    [Fact]
    public void The_Slots_Of_Each_Object_A_Pointer_Moves_Between_Follow_It()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10),
                                               MemoryHistoryStores.Block(Fourth, 0x10),
                                               MemoryHistoryStores.Block(Fifth, 0x10)],
                                              [Touch(Fourth),
                                               Touch(Fifth),
                                               (2, Second, Pointer(Fourth)),
                                               (2, Third, Pointer(Fifth)),
                                               (3, First, Pointer(Second)),
                                               (6, First, Pointer(Third))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10);

        Assert.Equal(3, graph.Nodes.Count);

        Assert.Equal([MemoryHistoryStores.GetBlock(store, Fourth), MemoryHistoryStores.GetBlock(store, Fifth)],
                     new ulong[] { 4, 7 }.Select(s => graph.Nodes[2].GetTarget(At(s))?.Block ?? -1));

        Assert.Equal([1, 0], graph.Edges.Select(e => e.ChangeCount));
    }

    [Fact]
    public void Hot_Pointers_Keep_The_Path_To_Them_And_Pointers_That_Never_Changed_Are_Dropped()
    {
        const ulong leaf = 0x6000;

        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10),
                                               MemoryHistoryStores.Block(Fourth, 0x10),
                                               MemoryHistoryStores.Block(leaf, 0x10)],
                                              [Touch(Third),
                                               Touch(Fourth),
                                               Touch(leaf),
                                               (2, First, Pointer(Second)),
                                               (2, First + 8, Pointer(leaf)),
                                               (3, Second, Pointer(Third)),
                                               (5, Second, Pointer(Fourth))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10)
            .GetHot(isReferenceHot: true, isDataHot: false);

        Assert.Equal([[First], [Second], [Third, Fourth]],
                     graph.Nodes.Select(n => n.Targets.Select(t => store.Blocks[t.Block].Address).ToArray()));

        Assert.Equal(1, graph.Edges.Single(e => e.To == 2).ChangeCount);
    }

    [Fact]
    public void Hot_Pointers_To_Objects_Already_Shown_Leave_Out_The_Path_Through_Them()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10),
                                               MemoryHistoryStores.Block(Fourth, 0x10)],
                                              [Touch(Third),
                                               Touch(Fourth),
                                               (2, First, Pointer(Third)),
                                               (2, First + 8, Pointer(Second)),
                                               (2, Second, Pointer(Third)),
                                               (2, Second + 8, Pointer(Fourth)),
                                               (4, First, Pointer(Fourth)),
                                               (5, Second, Pointer(Fourth)),
                                               (6, Second + 8, Pointer(Third))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10)
            .GetHot(isReferenceHot: true, isDataHot: false);

        Assert.Equal([[First], [Third, Fourth]],
                     graph.Nodes.Select(n => n.Targets.Select(t => store.Blocks[t.Block].Address).ToArray()));

        Assert.Equal([new MemoryHistoryGraphEdge(0, 1, 0, 0, 1)], graph.Edges);
    }

    [Fact]
    public void Objects_Pointing_At_The_Block_Expand_To_Its_Left_With_The_Objects_Pointing_At_Them()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10)],
                                              [Touch(First),
                                               (2, Second, Pointer(First)),
                                               (3, Third + 8, Pointer(Second))]);

        var graph = BuildReferrers(store, MemoryHistoryStores.GetBlock(store, First));

        Assert.Equal([(First, 0), (Second, -1), (Third, -2)],
                     graph.Nodes.Select(n => (store.Blocks[n.Targets.Single().Block].Address, n.Depth)));

        Assert.Equal([new MemoryHistoryGraphEdge(1, 0, 0, 0, 0), new(2, 1, 8, 0, 0)], graph.Edges);

        Assert.All(graph.Edges, e => Assert.True(graph.IsTreeEdge(e)));
    }

    [Fact]
    public void An_Object_Already_On_The_Graph_Is_Not_Added_Again_As_One_Pointing_At_It()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10), MemoryHistoryStores.Block(Second, 0x10)],
                                              [(2, First, Pointer(Second)), (3, Second + 8, Pointer(First))]);

        var graph = BuildReferrers(store, MemoryHistoryStores.GetBlock(store, First));

        Assert.Equal([0, 1], graph.Nodes.Select(n => n.Depth));
    }

    [Fact]
    public void Data_Changes_Keep_The_Objects_Whose_Other_Fields_Changed_And_Reference_Changes_Leave_Them_Out()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10)],
                                              [(2, First, Pointer(Second)),
                                               (2, First + 8, Pointer(Third)),
                                               (2, Second, Pointer(7)),
                                               (2, Third, Pointer(5)),
                                               (4, Third, Pointer(6))]);

        var graph = Build(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, First), 10);

        Assert.Equal([[First], [Third]], GetAddresses(store, graph.GetHot(isReferenceHot: false, isDataHot: true)));

        Assert.Equal([[First]], GetAddresses(store, graph.GetHot(isReferenceHot: true, isDataHot: false)));
    }

    [Fact]
    public void A_Box_Knows_Whether_It_Has_Pointers_To_Follow_And_Objects_Pointing_At_It()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(First, 0x10),
                                               MemoryHistoryStores.Block(Second, 0x10),
                                               MemoryHistoryStores.Block(Third, 0x10)],
                                              [Touch(Second), (2, First, Pointer(Second)), (3, Third, Pointer(First))]);

        var graph = MemoryHistoryGraph.Build(store,
                                             TraceBufferPool.Empty,
                                             MemoryHistoryStores.GetBlock(store, First),
                                             default,
                                             TracePosition.Unknown,
                                             TracePosition.Unknown,
                                             p => p.Length == 0,
                                             _ => false,
                                             8,
                                             10);

        Assert.Equal((true, true, false), (graph.Nodes[0].HasPointers, graph.Nodes[0].HasReferrers, graph.Nodes[1].HasPointers));
    }

    private static ulong[][] GetAddresses(MemoryHistoryStore store, MemoryHistoryGraph graph)
        => [.. graph.Nodes.Select(n => n.Targets.Select(t => store.Blocks[t.Block].Address).ToArray())];

    private static MemoryHistoryGraph Build(MemoryHistoryStore store,
                                            TraceBufferPool pool,
                                            int block,
                                            int limit,
                                            TracePosition? shutdown = null)
        => MemoryHistoryGraph.Build(store,
                                    pool,
                                    block,
                                    default,
                                    TracePosition.Unknown,
                                    shutdown ?? TracePosition.Unknown,
                                    _ => true,
                                    _ => false,
                                    depth: 8,
                                    limit);

    private static MemoryHistoryGraph BuildReferrers(MemoryHistoryStore store, int block)
        => MemoryHistoryGraph.Build(store,
                                    TraceBufferPool.Empty,
                                    block,
                                    default,
                                    TracePosition.Unknown,
                                    TracePosition.Unknown,
                                    _ => true,
                                    _ => true,
                                    8,
                                    10);

    private static byte[] Pointer(ulong value) => BitConverter.GetBytes(value);

    private static (ulong Sequence, ulong Address, byte[] Bytes) Touch(ulong address) => (1, address + 8, [1]);
}
