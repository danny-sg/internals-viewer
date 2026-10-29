using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Tests.Memory.History;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class BufferDescriptorTests
{
    private const ulong Buffer = 0x229_5796_2C40;

    [Theory]
    [InlineData(0x0000ul, BufferLatchState.Released)]
    [InlineData(0x1000ul, BufferLatchState.Shared)]
    [InlineData(0x0002ul, BufferLatchState.Exclusive)]
    [InlineData(0x000Aul, BufferLatchState.Waiting)]
    [InlineData(0x1018ul, BufferLatchState.Waiting)]
    public void The_Latch_State_Comes_From_The_State_Word(ulong state, BufferLatchState expected)
        => Assert.Equal(expected, BufferDescriptor.GetLatchState(state));

    [Fact]
    public void Shared_Holders_Are_Counted_Above_The_Flags()
        => Assert.Equal(2, BufferDescriptor.GetSharedCount(0x2010));

    [Fact]
    public void The_Latch_States_Follow_Every_Write_To_The_State_Word()
    {
        var store = MemoryHistoryStores.Build([MemoryHistoryStores.Block(Buffer, BufferDescriptor.Size)],
                                              [(2, Buffer + BufferDescriptor.LatchStateOffset, [0x02, 0, 0, 0, 0, 0, 0, 0]),
                                               (3, Buffer + BufferDescriptor.PageIdOffset, [0x2B, 0, 0, 0]),
                                               (4, Buffer + BufferDescriptor.LatchStateOffset, [0x00]),
                                               (5, Buffer + BufferDescriptor.LatchStateOffset + 1, [0x10])]);

        Assert.Equal([(0, 0x02ul), (2, 0ul), (3, 0x1000ul)], BufferDescriptor.GetLatchStates(store, 0));
    }
}
