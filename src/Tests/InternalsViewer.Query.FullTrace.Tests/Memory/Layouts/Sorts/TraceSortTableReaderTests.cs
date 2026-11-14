using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts.Enums;
using InternalsViewer.Query.FullTrace.Tests.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Sorts;

[Trait("Category", "Unit")]
public class TraceSortTableReaderTests
{
    private const ulong Iterator = 0x10000;

    private const ulong Rowset = 0x20000;

    private const ulong Descriptor = 0x30000;

    private const ulong RecordPage = 0x40000;

    private const ulong ComparePage = 0x50000;

    private const ulong EntrySize = 0x28;

    private const ulong First = ComparePage;

    private const ulong Second = ComparePage + EntrySize;

    private const ulong Third = ComparePage + 2 * EntrySize;

    [Fact]
    public void Entries_Are_Read_In_Page_Order_Until_The_First_Unused_Entry()
    {
        var table = Read();

        Assert.Equal([First, Second, Third], table.Entries.Select(e => e.Address));

        Assert.Equal((RecordPage + 0x60, RecordPage + 0x61, 11, 10),
                     table.Entries.Select(e => (e.Record, e.Key, e.RecordLength, e.KeyLength)).First());
    }

    [Fact]
    public void Runs_Are_Read_From_The_Descriptor_Fields_That_Point_To_Them()
        => Assert.Equal([("Run 1", Descriptor + 0x78, First), ("Current List", Descriptor + 0x178, Second)],
                        Read().Runs.Select(r => (r.Field.Name, r.Address, r.Head)));

    [Fact]
    public void Each_Run_Follows_Next_From_Its_First_Entry()
        => Assert.Equal([(0, 0), (1, 0), (0, 1)], Read().Entries.Select(e => (e.Run, e.SortedIndex)));

    [Fact]
    public void An_Entry_No_Run_Reaches_Is_In_No_Run()
    {
        var table = Read(currentList: 0);

        Assert.Equal(["Run 1"], table.Runs.Select(r => r.Field.Name));

        Assert.Equal([0, -1, 0], table.Entries.Select(e => e.Run));
    }

    [Fact]
    public void The_Cursor_Is_Read_From_The_Sort_Descriptor_And_Starts_A_Run()
    {
        var table = Read(currentList: 0, cursor: Second);

        Assert.Equal(Second, table.Cursor);

        Assert.Equal(["Run 1", "Cursor"], table.Runs.Select(r => r.Field.Name));
    }

    [Fact]
    public void A_Part_Is_Found_By_The_Block_That_Holds_It()
    {
        var table = Read();

        Assert.Equal(new TraceSortPart(TraceSortPartType.ComparePage, ComparePage), table.FindPart(ComparePage, 0x2000));

        Assert.Equal(new TraceSortPart(TraceSortPartType.Descriptor, Descriptor), table.FindPart(Descriptor, 0x260));

        Assert.Null(table.FindPart(0x90000, 0x100));
    }

    private static TraceSortTable Read(ulong currentList = Second, ulong cursor = 0)
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Iterator, 0x238),
                                                    MemoryHistoryStoreHelper.Block(Rowset, 0x470),
                                                    MemoryHistoryStoreHelper.Block(Descriptor, 0x260),
                                                    MemoryHistoryStoreHelper.Block(RecordPage, 0x2000),
                                                    MemoryHistoryStoreHelper.Block(ComparePage, 0x2000)],
                                                   [(1, Iterator + 0x48, Pointer(Rowset)),
                                                    (1, Rowset + 0x18, Pointer(Descriptor)),
                                                    (1, Descriptor + 0x78, Pointer(First)),
                                                    (1, Descriptor + 0x170, Pointer(cursor)),
                                                    (1, Descriptor + 0x178, Pointer(currentList)),
                                                    (1, RecordPage + 1, [7]),
                                                    .. Entry(First, RecordPage + 0x60, Third),
                                                    .. Entry(Second, RecordPage + 0x6B, 0),
                                                    .. Entry(Third, RecordPage + 0x76, 0)]);

        List<(int Block, TraceSortPartType Type)> pages =
        [
            (MemoryHistoryStoreHelper.GetBlock(store, RecordPage), TraceSortPartType.RecordPage),
            (MemoryHistoryStoreHelper.GetBlock(store, ComparePage), TraceSortPartType.ComparePage)
        ];

        return TraceSortTableReader.Read(store, pages, Iterator, At(5))!;
    }

    private static IEnumerable<(ulong Sequence, ulong Address, byte[] Bytes)> Entry(ulong entry, ulong record, ulong next)
    {
        yield return (1, entry + 0x08, Pointer(record));

        yield return (1, entry + 0x10, Pointer(record + 1));

        yield return (1, entry + 0x18, BitConverter.GetBytes((ushort)11));

        yield return (1, entry + 0x1A, BitConverter.GetBytes((ushort)10));

        yield return (1, entry + 0x20, Pointer(next));
    }

    private static byte[] Pointer(ulong value) => BitConverter.GetBytes(value);
}
