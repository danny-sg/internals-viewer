using System.Data;
using System.Text;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.HashRows;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class HashRowHookTests
{
    private const uint Thread = 7;

    private const ulong GetRow = 0x1000;

    private const ulong Lookup = 0x2000;

    private const ulong Insert = 0x3000;

    private const ulong HashIteratorTable = 0x7000;

    private const ulong Iterator = 0x1F4_0000_1000;

    private const ulong Context = 0x1F4_0000_8000;

    private const ulong PartitionGroup = 0x1F4_0000_A000;

    private const ulong Probe = 0x1F4_0000_B000;

    private const ulong Record = 0x1F4_0001_0000;

    private const int Slot = 2;

    private const uint EndStatus = 0x40EC6;

    private const int KeyEnd = 8;

    private static readonly ColumnStructure[] Columns = [Column("id", SqlDbType.Int, 4),
                                                         Column("name", SqlDbType.VarChar, 10),
                                                         Column("other", SqlDbType.Int, 4)];

    [Fact]
    public void A_Row_Returned_By_A_Hash_Match_Is_Read_From_The_Slot_Its_Lookup_Writes()
    {
        var row = Assert.Single(Capture(nullBitmap: 0, status: 0));

        Assert.Equal((RowBufferFormat.HashTable, Record, 22), (row.Format, row.Address, row.Record?.Length));
        Assert.Equal(row.Record, Assert.Single(row.Columns).Data);
    }

    [Fact]
    public void No_Row_Is_Read_When_The_Hash_Match_Returns_The_End()
        => Assert.Empty(Capture(nullBitmap: 0, status: EndStatus));

    [Fact]
    public void A_Row_The_Probe_Only_Partly_Sees_Is_Read_From_Its_Insert()
    {
        var row = Assert.Single(Capture(nullBitmap: 0, status: 0, isInserted: true));

        Assert.Equal(22, row.Record?.Length);
    }

    [Fact]
    public void A_Slot_That_Cannot_Be_Read_Is_The_Last_One_Read_For_Its_Partition_Group()
    {
        var replay = CreateReplay(BuildRecord(0));

        var hook = CreateHook();

        InsertRow(hook, replay);

        replay.Write(PartitionGroup + 0x08, 0UL);

        ReturnRow(hook, replay, status: 0);

        Assert.Single(hook.Rows);
    }

    [Fact]
    public void A_Layout_From_The_Plan_Splits_The_Record_Into_Its_Columns()
    {
        var store = new RowBufferStore(Capture(nullBitmap: 0, status: 0));

        store.ApplyRecordLayouts(GetColumn);

        var row = Assert.Single(store.All);

        Assert.Equal(["id", "name", "other"], row.RecordLayout?.Columns.Select(c => c.ColumnName));
        Assert.Equal([5, 9], new[] { row.Columns[0], row.Columns[2] }.Select(c => BitConverter.ToInt32(c.Data)));
        Assert.Equal("abc", Encoding.ASCII.GetString(row.Columns[1].Data));
        Assert.Equal([4, -1, 8], row.Columns.Select(c => c.Binding.RecordOffset));
    }

    [Fact]
    public void A_Build_Key_Stored_After_The_Columns_Is_Marked_As_A_Key()
    {
        var store = new RowBufferStore(Capture(nullBitmap: 0, status: 0));

        store.ApplyRecordLayouts(GetColumn, _ => 1);

        var layout = Assert.Single(store.All).RecordLayout;

        Assert.Equal([false, false, true], Enumerable.Range(0, 3).Select(o => layout?.IsKey(o) == true));
    }

    [Fact]
    public void A_Null_Column_Is_Read_From_The_Null_Bitmap()
    {
        var store = new RowBufferStore(Capture(nullBitmap: 0b010, status: 0));

        store.ApplyRecordLayouts(GetColumn);

        var column = Assert.Single(store.All).Columns[1];

        Assert.Equal((byte)3, column.Status);
        Assert.Empty(column.Data);
    }

    [Fact]
    public void A_Layout_That_Does_Not_Match_The_Record_Leaves_It_Whole()
    {
        var store = new RowBufferStore(Capture(nullBitmap: 0, status: 0));

        store.ApplyRecordLayouts((_, _) => Column("id", SqlDbType.BigInt, 8));

        Assert.Null(Assert.Single(store.All).RecordLayout);
    }

    [Fact]
    public void A_Column_Missing_From_The_Plan_Leaves_The_Record_Whole()
    {
        var store = new RowBufferStore(Capture(nullBitmap: 0, status: 0));

        store.ApplyRecordLayouts((i, o) => o == 0 ? GetColumn(i, o) : null);

        Assert.Single(Assert.Single(store.All).Columns);
    }

    [Fact]
    public void A_Hash_Row_Keeps_Its_Record_Through_Export()
    {
        var rows = Capture(nullBitmap: 0, status: 0);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            new RowBufferStore(rows).Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var read = Assert.Single(RowBufferStore.Read(reader).All);

        Assert.Equal(rows[0].Record, read.Record);
        Assert.Equal(RowBufferFormat.HashTable, read.Format);
    }

    private static IReadOnlyList<RowBuffer> Capture(byte nullBitmap, ulong status, bool isInserted = false)
    {
        var record = BuildRecord(nullBitmap);

        var hook = CreateHook();

        if (isInserted)
        {
            InsertRow(hook, CreateReplay(record));
        }

        ReturnRow(hook, CreateReplay(isInserted ? record[..KeyEnd] : record), status);

        return hook.Rows;
    }

    private static HashRowHook CreateHook() => new(new HashRowFunctions([GetRow], [Lookup], [Insert], [HashIteratorTable]));

    private static void InsertRow(HashRowHook hook, FakeTraceReplay replay)
    {
        hook.OnEntry(Call(Insert, 20, [PartitionGroup, Context, 0x1234, 0]), new FakeTraceReplay());

        hook.OnReturn(Call(Insert, 20, [PartitionGroup, Context, 0x1234, 0], returnValue: 1), replay);
    }

    private static void ReturnRow(HashRowHook hook, FakeTraceReplay replay, ulong status)
    {
        hook.OnEntry(Call(GetRow, 30, [Iterator, Context, 0, 0]), replay);

        hook.OnEntry(Call(Lookup, 31, [PartitionGroup, Context, 0x1234, 0]), new FakeTraceReplay());

        hook.OnReturn(Call(Lookup, 31, [PartitionGroup, Context, 0x1234, 0], returnValue: 1), replay);

        hook.OnReturn(Call(GetRow, 30, [Iterator, Context, 0, 0], returnValue: status), replay);
    }

    private static FakeTraceReplay CreateReplay(byte[] record)
        => new FakeTraceReplay().Write(Iterator, HashIteratorTable)
                                .Write(PartitionGroup + 0x08, Probe)
                                .Write(Probe + 0x14, Slot)
                                .Write(Context + Slot * sizeof(ulong), Record)
                                .Write(Record, record);

    private static byte[] BuildRecord(byte nullBitmap)
    {
        var isNameNull = (nullBitmap & 0b010) != 0;

        var record = new List<byte> { 0x30, 0x00, 12, 0x00 };

        record.AddRange(BitConverter.GetBytes(5));
        record.AddRange(BitConverter.GetBytes(9));
        record.AddRange(BitConverter.GetBytes((ushort)3));
        record.Add(nullBitmap);
        record.AddRange(BitConverter.GetBytes((ushort)1));
        record.AddRange(BitConverter.GetBytes((ushort)(isNameNull ? 19 : 22)));

        if (!isNameNull)
        {
            record.AddRange("abc"u8.ToArray());
        }

        return [.. record];
    }

    private static ColumnStructure? GetColumn(ulong iterator, int ordinal)
        => iterator == Iterator ? Columns.ElementAtOrDefault(ordinal) : null;

    private static ColumnStructure Column(string name, SqlDbType type, short length)
        => new() { ColumnName = name, DataType = type, DataLength = length };

    private static TraceHookCall Call(ulong function, ulong id, ulong[] arguments, ulong returnValue = 0)
        => new()
        {
            Function = function,
            Instance = Iterator,
            Id = id,
            Call = (uint)id,
            Position = At(id),
            Thread = Thread,
            Arguments = arguments,
            ReturnValue = returnValue
        };
}
