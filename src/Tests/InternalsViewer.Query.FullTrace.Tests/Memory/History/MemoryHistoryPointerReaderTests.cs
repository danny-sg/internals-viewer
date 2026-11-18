using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MemoryHistoryPointerReaderTests
{
    private const ulong Holder = 0x1000;

    private const ulong Target = 0x2000;

    private const ulong Frame = 0x20A_5448_0000;

    private const uint Thread = 9;

    [Fact]
    public void A_Pointer_To_A_Live_Block_Is_Found()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10), MemoryHistoryStoreHelper.Block(Target, 0x20)],
                                              [(2, Holder, Pointer(Target + 8)), (3, Target, [1])]);

        var target = MemoryHistoryStoreHelper.GetBlock(store, Target);

        Assert.Equal([new MemoryHistoryPointer(0, Target + 8, target, null)],
                     MemoryHistoryPointerReader.Read(store, TraceBufferPool.Empty, MemoryHistoryStoreHelper.GetBlock(store, Holder), Holder, At(5)));
    }

    [Fact]
    public void A_Pointer_Into_A_Freed_Block_Whose_Memory_Was_Reused_Is_Left_Out()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10),
                                               new MemoryBlock(Target, 0x20, Thread, At(1), At(4)),
                                               new MemoryBlock(Target, 0x20, Thread, At(5), TracePosition.Unknown)],
                                              [(2, Holder, Pointer(Target + 8)), (3, Target, [1]), (6, Target, [2])]);

        Assert.Empty(MemoryHistoryPointerReader.Read(store, TraceBufferPool.Empty, MemoryHistoryStoreHelper.GetBlock(store, Holder), Holder, At(7)));
    }

    [Fact]
    public void A_Pointer_Into_A_Frame_Is_Kept_Only_When_The_Frame_Held_The_Page_As_It_Was_Written()
    {
        var builder = new ReplayBufferPool();

        builder.AddSighting(new TraceBufferPage(Frame, new PageAddress(1, 100), 6, At(4)));

        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10)],
                                              [(2, Holder, Pointer(Frame + 0x60)), (5, Holder + 8, Pointer(Frame + 0x80))]);

        Assert.Equal([new MemoryHistoryPointer(8, Frame + 0x80, -1, new TraceBufferReference(new PageAddress(1, 100), 6, 0x80))],
                     MemoryHistoryPointerReader.Read(store, builder.Build(), MemoryHistoryStoreHelper.GetBlock(store, Holder), Holder, At(7)));
    }

    [Fact]
    public void A_Block_Holding_Only_Pointers_Is_Read_As_An_Array_Of_Them()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10),
                                                    MemoryHistoryStoreHelper.Block(Target, 0x20)],
                                              [(2, Holder, Pointer(Target)), (2, Holder + 8, Pointer(Target + 8)), (3, Target, [1])]);

        var array = MemoryHistoryPointerReader.ReadArray(store, TraceBufferPool.Empty, Holder, At(5));

        Assert.Equal([Target, Target + 8], array?.Select(p => p.Value));
    }

    [Fact]
    public void A_Block_With_A_Slot_That_Is_Not_A_Pointer_Is_Not_An_Array()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10),
                                                    MemoryHistoryStoreHelper.Block(Target, 0x20)],
                                              [(2, Holder, Pointer(Target)), (2, Holder + 8, Pointer(7)), (3, Target, [1])]);

        Assert.Null(MemoryHistoryPointerReader.ReadArray(store, TraceBufferPool.Empty, Holder, At(5)));
    }

    [Fact]
    public void Empty_Slots_After_The_Pointers_Do_Not_Stop_A_Block_Being_An_Array()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x20),
                                                    MemoryHistoryStoreHelper.Block(Target, 0x20)],
                                              [(2, Holder, Pointer(Target)),
                                               (2, Holder + 8, Pointer(Target + 8)),
                                               (2, Holder + 0x10, Pointer(0)),
                                               (3, Target, [1])]);

        var array = MemoryHistoryPointerReader.ReadArray(store, TraceBufferPool.Empty, Holder, At(5));

        Assert.Equal([Target, Target + 8], array?.Select(p => p.Value));
    }

    [Fact]
    public void A_Block_Of_Empty_Slots_Is_Not_An_Array()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10)], [(2, Holder, Pointer(0))]);

        Assert.Null(MemoryHistoryPointerReader.ReadArray(store, TraceBufferPool.Empty, Holder, At(5)));
    }

    [Fact]
    public void An_Address_Inside_A_Block_Is_Not_An_Array()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10),
                                                    MemoryHistoryStoreHelper.Block(Target, 0x20)],
                                              [(2, Holder, Pointer(Target)), (2, Holder + 8, Pointer(Target)), (3, Target, [1])]);

        Assert.Null(MemoryHistoryPointerReader.ReadArray(store, TraceBufferPool.Empty, Holder + 8, At(5)));
    }

    [Fact]
    public void A_Block_Cleared_Before_It_Is_Freed_Is_Read_As_The_Array_It_Last_Held()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x8),
                                                    MemoryHistoryStoreHelper.Block(Target, 0x20)],
                                                   [(2, Holder, Pointer(Target + 8)), (3, Target, [1]), (6, Holder, Pointer(0))]);

        var holder = MemoryHistoryStoreHelper.GetBlock(store, Holder);

        var array = MemoryHistoryPointerReader.ReadLastArray(store, TraceBufferPool.Empty, holder);

        Assert.Equal([Target + 8], array?.Pointers.Select(p => p.Value));

        Assert.Equal(At(2), array?.Position);
    }

    [Fact]
    public void A_Block_That_Never_Held_A_Pointer_Has_No_Last_Array()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x8)], [(2, Holder, Pointer(0))]);

        var holder = MemoryHistoryStoreHelper.GetBlock(store, Holder);

        Assert.Null(MemoryHistoryPointerReader.ReadLastArray(store, TraceBufferPool.Empty, holder));
    }

    [Fact]
    public void A_Pointer_To_Memory_Freed_Before_The_Position_Finds_The_Last_Block_There()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Target, 0x20, 1, 3),
                                                    MemoryHistoryStoreHelper.Block(Target, 0x20, 4, 6)],
                                                   [(2, Target, [1]), (5, Target, [2])]);

        var freed = store.GetFreedBlock(Target + 8, At(7));

        Assert.Equal(At(4), store.Blocks[freed].FromPosition);
    }

    [Fact]
    public void A_Pointer_To_Live_Or_Unfreed_Memory_Has_No_Freed_Block()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Target, 0x20, 1, 5)], [(2, Target, [1])]);

        Assert.Equal(-1, store.GetFreedBlock(Target, At(3)));

        Assert.Equal(-1, store.GetFreedBlock(Holder, At(7)));
    }

    private static byte[] Pointer(ulong value) => BitConverter.GetBytes(value);
}
