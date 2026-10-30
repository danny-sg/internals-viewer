using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MemoryHistoryPointersTests
{
    private const ulong Holder = 0x1000;

    private const ulong Target = 0x2000;

    private const ulong Frame = 0x20A_5448_0000;

    private const uint Thread = 9;

    [Fact]
    public void A_Pointer_To_A_Live_Block_Is_Found()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(Holder, 0x10), MemoryHistoryStores.Block(Target, 0x20)],
                                              [(2, Holder, Pointer(Target + 8)), (3, Target, [1])]);

        var target = MemoryHistoryStores.GetBlock(store, Target);

        Assert.Equal([new MemoryHistoryPointer(0, Target + 8, target, null)],
                     MemoryHistoryPointers.Read(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, Holder), Holder, At(5)));
    }

    [Fact]
    public void A_Pointer_Into_A_Freed_Block_Whose_Memory_Was_Reused_Is_Left_Out()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(Holder, 0x10),
                                               new MemoryBlock(Target, 0x20, Thread, At(1), At(4)),
                                               new MemoryBlock(Target, 0x20, Thread, At(5), TracePosition.Unknown)],
                                              [(2, Holder, Pointer(Target + 8)), (3, Target, [1]), (6, Target, [2])]);

        Assert.Empty(MemoryHistoryPointers.Read(store, TraceBufferPool.Empty, MemoryHistoryStores.GetBlock(store, Holder), Holder, At(7)));
    }

    [Fact]
    public void A_Pointer_Into_A_Frame_Is_Kept_Only_When_The_Frame_Held_The_Page_As_It_Was_Written()
    {
        var builder = new TraceBufferPool.Builder();

        builder.Add(new TraceBufferPage(Frame, new PageAddress(1, 100), 6, At(4)));

        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(Holder, 0x10)],
                                              [(2, Holder, Pointer(Frame + 0x60)), (5, Holder + 8, Pointer(Frame + 0x80))]);

        Assert.Equal([new MemoryHistoryPointer(8, Frame + 0x80, -1, new TraceBufferReference(new PageAddress(1, 100), 6, 0x80))],
                     MemoryHistoryPointers.Read(store, builder.Build(), MemoryHistoryStores.GetBlock(store, Holder), Holder, At(7)));
    }

    private static byte[] Pointer(ulong value) => BitConverter.GetBytes(value);
}
