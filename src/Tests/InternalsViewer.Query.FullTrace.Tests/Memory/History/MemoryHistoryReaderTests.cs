using InternalsViewer.Query.FullTrace.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.Memory.History.MemoryHistoryStores;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MemoryHistoryReaderTests
{
    private const ulong Owned = 0x21C_0BB5_0000;

    private static readonly MemoryHistoryStore Store = Build([Block(Owned, 0x10)],
                                                             [(2, Owned, [1, 2, 3, 4]), (4, Owned, [9])]);

    [Fact]
    public void Reads_The_Bytes_Written_Up_To_Its_Position()
    {
        var before = new byte[4];

        var after = new byte[4];

        new MemoryHistoryReader(Store, At(3)).Read(Owned, before);

        new MemoryHistoryReader(Store, At(5)).Read(Owned, after);

        Assert.Equal([1, 2, 3, 4], before);
        Assert.Equal([9, 2, 3, 4], after);
    }

    [Fact]
    public void A_Read_Stops_At_The_First_Byte_Never_Written()
        => Assert.Equal(4, new MemoryHistoryReader(Store, At(3)).Read(Owned, new byte[8]));

    [Fact]
    public void An_Address_Outside_Every_Live_Block_Reads_Nothing()
        => Assert.Equal(0, new MemoryHistoryReader(Store, At(3)).Read(Owned + 0x100, new byte[4]));

    [Fact]
    public void Has_Changed_Only_When_A_Block_It_Read_Was_Written_Again()
    {
        var reader = new MemoryHistoryReader(Store, At(3));

        reader.Read(Owned, new byte[4]);

        Assert.Equal((false, true), (reader.HasChanged(At(3, 5)), reader.HasChanged(At(5))));
    }
}
