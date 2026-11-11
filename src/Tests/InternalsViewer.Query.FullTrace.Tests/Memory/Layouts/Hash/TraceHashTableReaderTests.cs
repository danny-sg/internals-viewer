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

    [Fact]
    public void A_Group_Reads_Each_Column_From_Its_Value_Accessor_Bindings()
    {
        const ulong columnArray = 0x80000;
        const ulong scanArray = 0x81000;
        const ulong value = 0x82000;
        const ulong scanValue = 0x83000;
        const ulong accessor = 0x84000;
        const ulong bindings = 0x85000;
        const ulong binding = 0x86000;

        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Group, 0x200),
                                                    MemoryHistoryStoreHelper.Block(columnArray, 0x8),
                                                    MemoryHistoryStoreHelper.Block(scanArray, 0x8),
                                                    MemoryHistoryStoreHelper.Block(value, 0x58),
                                                    MemoryHistoryStoreHelper.Block(scanValue, 0x58),
                                                    MemoryHistoryStoreHelper.Block(accessor, 0x20),
                                                    MemoryHistoryStoreHelper.Block(bindings, 0x110),
                                                    MemoryHistoryStoreHelper.Block(binding, 0x80)],
                                                   [(1, Group + 0x10, Pointer(0x70000)),
                                                    (1, Group + TraceHashTableLayout.ColumnsOffset, Pointer(columnArray)),
                                                    (1, Group + TraceHashTableLayout.ScanColumnsOffset, Pointer(scanArray)),
                                                    (1, columnArray, Pointer(value)),
                                                    (1, scanArray, Pointer(scanValue)),
                                                    (1, value + TraceHashTableLayout.ValueAccessorOffset, Pointer(accessor)),
                                                    (1, accessor, Pointer(bindings)),
                                                    (1, bindings + 0x62, BitConverter.GetBytes((ushort)1)),
                                                    (1, bindings + 0x70, Pointer(binding)),
                                                    (1, binding, BitConverter.GetBytes(12)),
                                                    (1, binding + 0x04, BitConverter.GetBytes((ushort)3)),
                                                    (1, binding + 0x28, BitConverter.GetBytes(8)),
                                                    (1, binding + 0x30, BitConverter.GetBytes(0)),
                                                    (1, binding + 0x3C, BitConverter.GetBytes((ushort)8)),
                                                    (1, binding + 0x3E, [0x7F]),
                                                    (1, binding + 0x40, BitConverter.GetBytes(0x81601u))]);

        var column = Assert.Single(TraceHashTableReader.Read(store, Group, At(2))!.Columns);

        Assert.Equal((3, 12, (byte)0x7F, 8), (column.Binding.Ordinal, column.Binding.RecordOffset, column.Binding.Type, column.Binding.MaximumLength));

        Assert.Equal((value, scanValue, accessor, bindings), (column.Value, column.ScanValue, column.Accessor, column.Bindings));
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
