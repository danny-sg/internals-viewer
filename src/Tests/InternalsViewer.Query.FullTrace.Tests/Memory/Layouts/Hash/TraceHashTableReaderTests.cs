using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Tests.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Hash;

[Trait("Category", "Unit")]
public class TraceHashTableReaderTests
{
    private const ulong Group = 0x10000;

    private const ulong Partition = 0x20000;

    private const ulong Directory = 0x30000;

    private const ulong PointerPage = 0x40000;

    private const ulong BucketPage = 0x50000;

    private const ulong OverflowPage = 0x60000;

    private const ulong Slot = BucketPage + 3 * TraceHashTableLayout.BucketSize;

    private const ulong Overflow = OverflowPage + 0x80;

    [Fact]
    public void A_Bucket_Holds_The_Rows_In_Its_Head_Item_Then_In_Each_Overflow_Item()
    {
        var bucket = Assert.Single(Read(Slot).Partitions.Single().Buckets);

        Assert.Equal(3, bucket.Index);

        Assert.Equal([Slot, Overflow], bucket.Items.Select(i => i.Address));

        Assert.Equal([new TraceHashEntry(11, 0xA0), new(12, 0xB0), new(13, 0xC0)], bucket.Entries);
    }

    [Fact]
    public void A_Bucket_Whose_Slot_Holds_Another_Buckets_Item_Starts_From_Its_Head()
    {
        var bucket = Assert.Single(Read(Overflow + 0x80).Partitions.Single().Buckets);

        Assert.Equal([Overflow + 0x80, Overflow], bucket.Items.Select(i => i.Address));
    }

    private static TraceHashTable Read(ulong head)
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Group, 0x200),
                                                    MemoryHistoryStoreHelper.Block(Partition, 0x100),
                                                    MemoryHistoryStoreHelper.Block(Directory, 0x10),
                                                    MemoryHistoryStoreHelper.Block(PointerPage, 0x2000),
                                                    MemoryHistoryStoreHelper.Block(BucketPage, 0x2000),
                                                    MemoryHistoryStoreHelper.Block(OverflowPage, 0x2000)],
                                                   [(1, Group + 0x10, Pointer(0x70000)),
                                                    (1, Group + 0xF8, Pointer(Partition + 0x98)),
                                                    (1, Partition + 0xA0, Pointer(Group + 0xF0)),
                                                    (1, Partition + 0x48, Pointer(Directory)),
                                                    (1, Partition + 0x50, BitConverter.GetBytes(1u)),
                                                    (1, Partition + 0x54, BitConverter.GetBytes(0x3Fu)),
                                                    (1, Directory, Pointer(PointerPage)),
                                                    (1, PointerPage, Pointer(BucketPage)),
                                                    (1, Slot + TraceHashTableLayout.HeadOffset, Pointer(head)),
                                                    .. Item(head, Overflow, (11, 0xA0), (12, 0xB0)),
                                                    .. Item(Overflow, 0, (13, 0xC0))]);

        return TraceHashTableReader.Read(store, Group, At(2))!;
    }

    private static (ulong Sequence, ulong Address, byte[] Bytes)[] Item(ulong address, ulong next, params (uint Key, ulong Row)[] entries)
        =>
        [
            (1, address, [(byte)entries.Length]),
            (1, address + TraceHashTableLayout.NextOffset, Pointer(next)),
            .. entries.SelectMany((e, i) => new (ulong, ulong, byte[])[]
            {
                (1, address + (ulong)TraceHashTableLayout.GetKeyOffset(i), BitConverter.GetBytes(e.Key)),
                (1, address + (ulong)TraceHashTableLayout.GetRowOffset(i), Pointer(e.Row))
            })
        ];

    private static byte[] Pointer(ulong value) => BitConverter.GetBytes(value);
}
