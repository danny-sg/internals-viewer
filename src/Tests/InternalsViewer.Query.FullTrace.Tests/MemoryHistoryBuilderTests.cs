using System.Text;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryHistoryBuilderTests
{
    private const ulong Page = 0x21C_0BB4_C000;

    private const ulong Iterator = 0x21C_0BB4_C060;

    private const ulong Owned = 0x21C_0BB5_0000;

    private const ulong Writer = 0x7FF8_0080_0010;

    private const uint Thread = 9;

    [Fact]
    public void A_First_Write_Initialises_Bytes_And_Later_Writes_Change_Them()
    {
        var store = Build([Block(Owned, 0x10, from: 1)], [Write(2, Owned, 0, 0, 0, 0), Write(3, Owned + 2, 5)]);

        var writes = store.GetWrites(0);

        Assert.Equal([MemoryHistoryChangeType.Initialised, MemoryHistoryChangeType.Changed],
                     writes.Select(m => store.GetChanges(m)[0].Type));
        Assert.Equal(2, store.GetChanges(writes[1])[0].Offset);
        Assert.Equal([5], store.GetBytes(store.GetChanges(writes[1])[0]).ToArray());
    }

    [Fact]
    public void A_Write_That_Leaves_The_Value_Alone_Is_Not_Kept()
    {
        var store = Build([Block(Owned, 0x10, from: 1)], [Write(2, Owned, 7), Write(3, Owned, 7)]);

        Assert.Single(store.GetWrites(0));
    }

    [Fact]
    public void A_Write_Belongs_To_The_Innermost_Live_Allocation()
    {
        var store = Build([Block(Page, 0x2000, from: 1), Block(Page + 0x100, 0x20, from: 2)],
                          [Write(3, Page + 0x108, 1), Write(4, Page + 0x10, 2)]);

        Assert.Equal([(Page, 0x10), (Page + 0x100, 8)],
                     store.Blocks.Select((b, index) => (b.Address, store.GetChanges(Assert.Single(store.GetWrites(index)))[0].Offset)));
    }

    [Fact]
    public void A_Write_Across_Two_Allocations_Is_Split_Between_Them()
    {
        var store = Build([Block(Owned, 8, from: 1), Block(Owned + 8, 8, from: 1)], [Write(2, Owned + 6, 1, 2, 3, 4)]);

        Assert.Equal([(6, 2), (0, 2)],
                     store.Blocks.Select((_, index) => store.GetChanges(Assert.Single(store.GetWrites(index)))[0])
                          .Select(c => (c.Offset, c.Length)));
    }

    [Fact]
    public void Writes_Outside_An_Allocation_Lifetime_Are_Not_Kept()
    {
        var store = Build([Block(Owned, 0x10, from: 2, to: 4)], [Write(1, Owned, 1), Write(3, Owned, 2), Write(5, Owned, 3)]);

        Assert.Equal(At(3), Assert.Single(store.GetWrites(0)).Position);
    }

    [Fact]
    public void An_Allocation_Ends_When_The_Allocation_Holding_It_Is_Freed()
    {
        var store = Build([Block(Page, 0x2000, from: 1, to: 5), Block(Page + 0x100, 0x20, from: 2)], [Write(6, Page + 0x100, 1)]);

        Assert.Empty(store.Blocks);
    }

    [Fact]
    public void An_Iterator_Inside_A_Larger_Allocation_Gets_A_Block_To_Its_End()
    {
        var store = Build([Block(Page, 0x2000, from: 1)],
                          [Write(3, Iterator + 8, 1)],
                          iterators: new Dictionary<ulong, TracePosition> { [Iterator] = At(2) });

        var block = Assert.Single(store.Blocks);

        Assert.Equal((Iterator, Iterator, 0x2000 - 0x60, true, true),
                     (block.Iterator, block.Address, block.Size, block.IsIterator, block.IsInline));
    }

    [Fact]
    public void An_Iterator_Ends_Where_The_Next_Iterator_In_The_Same_Allocation_Starts()
    {
        var store = Build([Block(Page, 0x2000, from: 1)],
                          [Write(3, Iterator + 8, 1), Write(4, Iterator + 0x108, 1)],
                          iterators: new Dictionary<ulong, TracePosition> { [Iterator] = At(2), [Iterator + 0x100] = At(2) });

        Assert.Equal([(Iterator, 0x100), (Iterator + 0x100, 0x2000 - 0x160)], store.Blocks.Select(b => (b.Address, b.Size)));
    }

    [Fact]
    public void An_Allocation_Owned_By_Itself_Is_An_Iterator()
    {
        var store = Build([Block(Iterator, 0x258, from: 1), Block(Owned, 0x10, from: 2)],
                          [Write(3, Iterator, 1), Write(4, Owned, 1)],
                          owners: [Iterator, Iterator]);

        Assert.Equal([(Iterator, true), (Iterator, false)], store.Blocks.Select(b => (b.Iterator, b.IsIterator)));
    }

    [Fact]
    public void Allocations_Without_Writes_Are_Not_Kept()
    {
        var store = Build([Block(Owned, 0x10, from: 1), Block(Owned + 0x100, 0x10, from: 1)], [Write(2, Owned, 1)]);

        Assert.Equal(Owned, Assert.Single(store.Blocks).Address);
    }

    [Fact]
    public void Overlapping_Allocations_Are_Watched_As_One_Range()
    {
        var ranges = MemoryHistoryBuilder.GetRanges([Block(Page, 0x2000, from: 1),
                                                     Block(Page + 0x100, 0x20, from: 2),
                                                     Block(Owned, 0x10, from: 3)]);

        Assert.Equal([new WatchRange(Page, 0x2000), new WatchRange(Owned, 0x10)], ranges);
    }

    [Fact]
    public void Each_Call_That_Changes_A_Value_Is_A_Row_In_Position_Order()
    {
        var (timeline, nodes) = BuildTimeline();

        var store = Build([Block(Owned, 0x10, from: 1)],
                          [Write(5, Owned, new byte[0x10]), Write(15, Owned + 8, 1), Write(35, Owned + 8, 2), Write(50, Owned, 9)],
                          timeline: timeline);

        var calls = store.GetCalls(0, timeline, default, TracePosition.Unknown).Skip(1).ToList();

        Assert.Equal([(nodes.Scanner, Iterator, 0, At(15)), (nodes.Scanner, Iterator, 1, At(35)), (nodes.Query, 0ul, 0, At(50))],
                     calls.Select(c => (c.Node, c.Iterator, c.CallIndex, c.Position)));
        Assert.Equal([(byte)1, (byte)2, (byte)2], calls.Select(c => store.GetImage(0, c.LastWrite).Data[8]));
    }

    [Fact]
    public void A_Call_That_Puts_A_Value_Back_Has_No_Net_Change()
    {
        var (timeline, _) = BuildTimeline();

        var store = Build([Block(Owned, 0x10, from: 1)],
                          [Write(5, Owned, new byte[0x10]), Write(13, Owned + 8, 5), Write(14, Owned + 8, 0), Write(35, Owned + 8, 2)],
                          timeline: timeline);

        var calls = store.GetCalls(0, timeline, default, TracePosition.Unknown).Skip(1);

        Assert.Equal([(2, 1, 0), (1, 1, 1)], calls.Select(c => (c.ChangeCount, c.Regions.Count, c.NetRegions.Count)));
    }

    [Fact]
    public void The_Allocation_Is_The_First_Row_With_The_State_Before_The_First_Change()
    {
        var (timeline, nodes) = BuildTimeline();

        var store = Build([Block(Owned, 0x10, from: 24)], [Write(26, Owned, new byte[0x10]), Write(35, Owned + 8, 2)], timeline: timeline);

        var allocation = store.GetCalls(0, timeline, default, TracePosition.Unknown)[0];

        Assert.Equal((true, nodes.GetRow, 2, At(24), 0, 1), (allocation.IsAllocation,
                                                                allocation.Node,
                                                                allocation.CallIndex,
                                                                allocation.Position,
                                                                allocation.LastWrite,
                                                                allocation.ChangeCount));
        Assert.Equal([new MemoryHistoryRegion(0, 0x10)], allocation.Regions);
    }

    [Fact]
    public void An_Iterator_Inside_A_Larger_Allocation_Starts_With_Its_First_Writer()
    {
        var (timeline, nodes) = BuildTimeline();

        var store = Build([Block(Page, 0x2000, from: 1)],
                          [Write(15, Iterator, 0, 0, 0, 0), Write(35, Iterator, 7)],
                          iterators: new Dictionary<ulong, TracePosition> { [Iterator] = At(2) },
                          timeline: timeline);

        var allocation = store.GetCalls(0, timeline, default, TracePosition.Unknown)[0];

        Assert.Equal((true, nodes.Scanner, 0, At(15)),
                     (allocation.IsAllocation, allocation.Node, allocation.CallIndex, allocation.Position));
    }

    [Fact]
    public void Calls_Outside_The_Window_Are_Left_Out()
    {
        var (timeline, _) = BuildTimeline();

        var store = Build([Block(Owned, 0x10, from: 1)],
                          [Write(5, Owned, new byte[0x10]), Write(15, Owned + 8, 1), Write(35, Owned + 8, 2)],
                          timeline: timeline);

        Assert.Equal(1, Assert.Single(store.GetCalls(0, timeline, At(30), At(40)).Skip(1)).CallIndex);
    }

    [Fact]
    public void A_Summary_Counts_Every_Change_And_Names_The_Operators_On_Their_Stacks()
    {
        var (timeline, _) = BuildTimeline();

        var store = Build([Block(Owned, 0x10, from: 1)],
                          [Write(5, Owned, new byte[0x10]), Write(15, Owned + 8, 1), Write(35, Owned + 8, 2), Write(50, Owned, 9)],
                          timeline: timeline);

        var summary = store.GetSummary(0, default, TracePosition.Unknown);

        Assert.Equal([Iterator], summary.Operators);
        Assert.Equal((3, 3, 2), (summary.CallCount, summary.ChangeCount, summary.ChangingByteCount));
    }

    [Fact]
    public void An_Object_Is_Created_At_Its_Allocation_Or_Its_First_Write_When_Inline()
    {
        var store = Build([Block(Page, 0x2000, from: 1), Block(Owned, 0x10, from: 20)],
                          [Write(15, Iterator, 1), Write(25, Owned, 1)],
                          iterators: new Dictionary<ulong, TracePosition> { [Iterator] = At(2) });

        Assert.Equal([(Owned, At(20), true), (Iterator, At(15), false)],
                     store.Blocks.Select((b, index) => (b.Address,
                                                        store.GetCreatedPosition(index),
                                                        store.IsCreatedWithin(index, At(18), At(30)))));
    }

    [Fact]
    public void Initialised_Bytes_Make_No_Call()
    {
        var (timeline, _) = BuildTimeline();

        var store = Build([Block(Owned, 0x10, from: 1)], [Write(15, Owned, 1)], timeline: timeline);

        Assert.Empty(store.GetCalls(0, timeline, default, TracePosition.Unknown));
        Assert.Equal(0, store.GetSummary(0, default, TracePosition.Unknown).ChangeCount);
    }

    [Fact]
    public void An_Allocation_Is_Made_By_The_First_Call_Outside_The_Memory_Functions()
    {
        var (timeline, nodes) = BuildTimeline();

        var store = Build([Block(Owned, 0x10, from: 24)], [Write(26, Owned, 1)], timeline: timeline);

        Assert.Same(nodes.GetRow, store.GetAllocatingCall(0, timeline));
    }

    [Fact]
    public void Writes_To_Clerk_Pages_And_Allocations_Under_The_Buffer_Pool_Are_Not_Kept()
    {
        var (timeline, _) = BuildTimeline();

        var store = Build([Block(Owned, 0x10, from: 1),
                           Block(Page, 0x2000, from: 1),
                           Block(Page + 0x100, 0x20, from: 2),
                           Block(Owned + 0x100, 0x10, from: 62)],
                          [Write(2, Owned, 1), Write(3, Page, 1), Write(4, Page + 0x108, 1), Write(63, Owned + 0x100, 1)],
                          timeline: timeline,
                          clerkPages: [Page]);

        Assert.Equal([Owned, Page + 0x100], store.Blocks.Select(b => b.Address));
    }

    [Fact]
    public void An_Image_Is_Rebuilt_From_The_Changes_Up_To_A_Write()
    {
        var store = Build([Block(Owned, 0x10, from: 1)], [Write(2, Owned, new byte[0x10]), Write(3, Owned + 8, 7)]);

        Assert.Equal(((byte)0, (byte)7), (store.GetImage(0, 0).Data[8], store.GetImage(0, 1).Data[8]));

        var changing = store.GetChangingBytes(0);

        Assert.Equal([8], Enumerable.Range(0, changing.Length).Where(i => changing[i]));
    }

    [Fact]
    public void The_Changes_Are_Rebuilt_From_What_Is_Written()
    {
        var (timeline, _) = BuildTimeline();

        var built = Build([Block(Owned, 0x10, from: 1)], [Write(5, Owned, new byte[0x10]), Write(15, Owned + 8, 7)], timeline: timeline);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            built.Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var store = MemoryHistoryStore.Read(reader);

        var write = store.GetWrites(0)[1];

        Assert.Equal((1, 2, (byte)7), (store.Blocks.Count, store.WriteCount, store.GetImage(0, 1).Data[8]));
        Assert.Equal((2, 0, Iterator), (write.CallNodeIndex, write.CallIndex, store.GetIterator(write)));
        Assert.Equal((built.Blocks[0].CallNodeIndex, Thread), (store.Blocks[0].CallNodeIndex, store.Blocks[0].Thread));
    }

    [Fact]
    public void A_Pointer_Finds_The_Innermost_Block_Live_At_The_Position()
    {
        var store = Build([Block(Page, 0x2000, from: 1, to: 9), Block(Page + 0x100, 0x20, from: 2, to: 6)],
                          [Write(3, Page + 0x108, 1), Write(4, Page + 0x10, 2)]);

        Assert.Equal([1, 0, 0, -1, -1, -1],
                     [store.GetBlock(Page + 0x110, At(3)),
                      store.GetBlock(Page + 0x110, At(7)),
                      store.GetBlock(Page + 0x10, At(3)),
                      store.GetBlock(Page + 0x2000, At(3)),
                      store.GetBlock(Page + 0x10, At(0)),
                      store.GetBlock(Page + 0x10, At(9))]);
    }

    [Fact]
    public void The_Image_At_A_Position_Includes_The_Writes_Made_At_It()
    {
        var store = Build([Block(Owned, 0x10, from: 1)], [Write(2, Owned, 1), Write(4, Owned, 2), Write(6, Owned, 3)]);

        Assert.Equal([0, 1, 2, 3], [.. new ulong[] { 1, 2, 5, 9 }.Select(s => store.GetWriteCount(0, At(s)))]);
        Assert.Equal(2, store.GetImage(0, store.GetWriteCount(0, At(5)) - 1).Data[0]);
    }

    [Fact]
    public void Writes_From_Later_Batches_Are_Applied_In_Position_Order()
    {
        var watched = new WatchResult([new WatchBatch([WatchedHit.From(Hit(5, Owned, 1), 0)], [2]),
                                       new WatchBatch([WatchedHit.From(Hit(3, Owned, 1), 0)], [1])],
                                      1);

        var store = MemoryHistoryBuilder.Build([new MemoryHistoryAllocation(Block(Owned, 0x10, from: 1), false)],
                                               [0],
                                               new Dictionary<ulong, TracePosition>(),
                                               watched,
                                               null);

        Assert.Equal([(At(3), MemoryHistoryChangeType.Initialised), (At(5), MemoryHistoryChangeType.Changed)],
                     store.GetWrites(0).Select(w => (w.Position, store.GetChanges(w)[0].Type)));
    }

    [Fact]
    public void Writes_From_Interleaved_Threads_Are_Merged_In_Position_Order()
    {
        var other = new WatchHit(4, 0, Owned + 1, Writer, Thread + 1, 1, 0, 1);

        var watched = new WatchResult([new WatchBatch([WatchedHit.From(Hit(2, Owned, 1), 0), WatchedHit.From(Hit(6, Owned, 1), 1)], [1, 3]),
                                       new WatchBatch([WatchedHit.From(other, 0)], [2])],
                                      1);

        var store = MemoryHistoryBuilder.Build([new MemoryHistoryAllocation(Block(Owned, 0x10, from: 1), false)],
                                               [0],
                                               new Dictionary<ulong, TracePosition>(),
                                               watched,
                                               null);

        Assert.Equal([At(2), At(4), At(6)], store.GetWrites(0).Select(w => w.Position));
        Assert.Equal(new byte[] { 3, 2 }, store.GetImage(0, 2).Data[..2]);
    }

    [Fact]
    public void The_Watch_Keeps_Only_Writes_That_Touch_A_Live_Allocation()
    {
        var keep = MemoryHistoryBuilder.GetWatchFilter([new MemoryHistoryAllocation(Block(Owned, 0x10, from: 2, to: 6), false)]);

        Assert.Equal([false, true, true, false, false],
                     [keep(Hit(1, Owned, 4)),
                      keep(Hit(3, Owned + 0xC, 4)),
                      keep(Hit(3, Owned - 2, 4)),
                      keep(Hit(3, Owned + 0x10, 4)),
                      keep(Hit(6, Owned, 4))]);
    }

    private static (TraceTimeline Timeline, (CallStackNode Query, CallStackNode GetRow, CallStackNode Scanner) Nodes) BuildTimeline()
    {
        var tree = new CallStackTree();

        var query = tree.AddCall(tree.Root, Frame("CQueryScan", "GetRow", 0x10), 1);
        var getRow = tree.AddCall(query, Frame("CQScanRangeNew", "GetRow", 0x20, Iterator), 3);
        var scanner = tree.AddCall(getRow, Frame("IndexRowScanner", "MoveNext", 0x30), 2);
        var pages = tree.AddCall(getRow, Frame("CMemThread<CMemObj>", "PbGetNewPages", 0x40), 1);
        var bufferPool = tree.AddCall(query, Frame("BPool", "Get", 0x50), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100, 0),
                     Span(1, 10, 20, 0),
                     Span(2, 12, 18, 0),
                     Span(1, 30, 40, 1),
                     Span(2, 32, 38, 1),
                     Span(1, 22, 28, 2),
                     Span(3, 23, 27, 0),
                     Span(4, 60, 70, 0)]);

        var timeline = builder.Build([new(-1, 0, 0x10, 0, 1),
                                      new(0, 0, 0x20, Iterator, 3),
                                      new(1, 0, 0x30, 0, 2),
                                      new(1, 0, 0x40, 0, 1),
                                      new(0, 0, 0x50, 0, 1)]);

        timeline.MapNodes([query, getRow, scanner, pages, bufferPool]);

        return (timeline, (query, getRow, scanner));
    }

    private static CallstackFrame Frame(string className, string method, uint rva, ulong instance = 0) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Address = rva,
        InstanceAddress = instance,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = SymbolCategory.Unknown
        }
    };

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end, uint callIndex)
        => new(start, 0, end, 0, start, end, callNodeIndex, Thread, callIndex, 1);

    private static MemoryBlock Block(ulong pointer, ulong bytes, ulong from, ulong? to = null)
        => new(pointer, bytes, Thread, At(from), to is { } end ? At(end) : TracePosition.Unknown);

    private static WatchHit Hit(ulong sequence, ulong address, uint size) => new(sequence, 0, address, Writer, Thread, size, 0, 1);

    private static (ulong Sequence, ulong Address, byte[] Bytes) Write(ulong sequence, ulong address, params byte[] bytes)
        => (sequence, address, bytes);

    private static MemoryHistoryStore Build(MemoryBlock[] blocks,
                                            (ulong Sequence, ulong Address, byte[] Bytes)[] writes,
                                            ulong[]? owners = null,
                                            Dictionary<ulong, TracePosition>? iterators = null,
                                            TraceTimeline? timeline = null,
                                            ulong[]? clerkPages = null)
    {
        var data = new List<byte>();

        var hits = new List<WatchedHit>();

        foreach (var (sequence, address, bytes) in writes)
        {
            hits.Add(WatchedHit.From(new WatchHit(sequence, 0, address, Writer, Thread, (uint)bytes.Length, 0, 1), data.Count));

            data.AddRange(bytes);
        }

        return MemoryHistoryBuilder.Build([.. blocks.Select(b => new MemoryHistoryAllocation(b, clerkPages?.Contains(b.Pointer) ?? false))],
                                          owners ?? new ulong[blocks.Length],
                                          iterators ?? new Dictionary<ulong, TracePosition>(),
                                          new WatchResult([new WatchBatch([.. hits], [.. data])], 1),
                                          timeline);
    }
}
