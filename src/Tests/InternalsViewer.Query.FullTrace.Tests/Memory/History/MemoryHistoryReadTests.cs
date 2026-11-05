using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
using static InternalsViewer.Query.FullTrace.Tests.Memory.History.MemoryHistoryStoreHelper;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MemoryHistoryReadTests
{
    private const ulong Owned = 0x2000;

    private const ulong Unwritten = 0x3000;

    [Fact]
    public void Reads_In_One_Call_Become_One_Record_Spanning_The_Bytes_Read()
    {
        var store = BuildWithReads([Block(Owned, 0x10)],
                                   [(2, Owned, [1])],
                                   [(3, Owned + 8, 4), (4, Owned, 2), (12, Owned + 4, 4)],
                                   MemoryHistoryStoreHelper.Calls((1, 10), (11, 20)));

        var reads = store.GetReads(GetBlock(store, Owned));

        Assert.Equal([(At(3), 0, 0xC, 2, 0), (At(12), 4, 4, 1, 1)],
                     reads.Select(r => (r.Position, r.Offset, r.Length, r.Count, r.CallIndex)));
    }

    [Fact]
    public void Reads_Outside_A_Block_Are_Not_Kept_But_A_Block_That_Is_Only_Read_Is()
    {
        var store = BuildWithReads([Block(Owned, 0x10), Block(Unwritten, 0x10)],
                                   [(2, Owned, [1])],
                                   [(3, Unwritten, 8), (4, Owned + 0x100, 8)],
                                   MemoryHistoryStoreHelper.Calls((1, 10)));

        var unwritten = MemoryHistoryStoreHelper.GetBlock(store, Unwritten);

        Assert.Equal((1, 0, 1), (store.ReadCount, store.Blocks[unwritten].WriteCount, store.GetReads(unwritten).Count));
    }

    [Fact]
    public void A_Late_Read_From_A_Finished_Call_Joins_Its_Record()
    {
        var blocks = Enumerable.Range(0, 80).Select(i => Block(Owned + (ulong)i * 0x100, 0x10)).ToArray();

        (ulong Sequence, ulong Address, uint Size)[] reads =
        [
            (12, Owned + 79 * 0x100, 4),
            .. Enumerable.Range(0, 70).Select(i => (2ul, Owned + (ulong)i * 0x100, 4u)),
            (3, Owned, 4)
        ];

        var store = BuildWithReads(blocks, [], reads, MemoryHistoryStoreHelper.Calls((1, 10), (11, 20)));

        var read = Assert.Single(store.GetReads(GetBlock(store, Owned)));

        Assert.Equal((At(2), 0, 2), (read.Position, read.CallIndex, read.Count));
    }

    [Fact]
    public void Reads_Survive_An_Export_Round_Trip()
    {
        var timeline = MemoryHistoryStoreHelper.Calls((1, 10));

        var store = BuildWithReads([Block(Owned, 0x10)], [(2, Owned, [1])], [(3, Owned + 8, 4)], timeline);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            store.Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var imported = MemoryHistoryStore.Read(reader, timeline);

        Assert.Equal(store.GetReads(0), imported.GetReads(0));
        Assert.Equal(store.GetReadPoint(0), imported.GetReadPoint(0));
    }
}
