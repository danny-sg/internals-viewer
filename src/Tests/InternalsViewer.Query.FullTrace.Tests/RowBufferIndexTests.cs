using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.RowBuffers;

using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class RowBufferIndexTests
{
    private const ulong Function = 0x7FF8_0010_0000;

    private const ulong DirectSelect = 0x7FF8_0010_0400;

    private const ulong Instance = 0x1F4_EEAE_1820;

    private const ulong Iterator = Instance;

    private const ulong Buffer = 0x1F4_EEAE_2000;

    private const uint FixedBinding = 0x2;

    private const uint VariableBinding = 0x902;

    [Fact]
    public void A_Call_Reads_Back_Its_Row()
    {
        var row = Assert.IsType<RowBuffer>(Build().GetRow(Function, Instance, 1));

        Assert.Equal((Buffer, Iterator, 100ul, 1), (row.Address, row.Iterator, row.Id, row.Call));
        Assert.Equal([1, 0, 0, 0], row.Columns[0].Data);
        Assert.Equal("First", Encoding.ASCII.GetString(row.Columns[1].Data));
    }

    [Fact]
    public void Columns_Keep_Their_Bindings()
    {
        var columns = Build().GetRow(Function, Instance, 1)!.Columns;

        Assert.Equal((4, 4, FixedBinding, false), Describe(columns[0]));
        Assert.Equal((-1, 8000, VariableBinding, true), Describe(columns[1]));
    }

    [Fact]
    public void Rows_Are_Found_By_Call_Whatever_Order_They_Arrive_In()
    {
        var index = Build();

        Assert.Equal("Second", Encoding.ASCII.GetString(index.GetRow(Function, Instance, 2)!.Columns[1].Data));
        Assert.Equal("First", Encoding.ASCII.GetString(index.GetRow(Function, Instance, 1)!.Columns[1].Data));
        Assert.Equal([1, 2], index.GetRows(Function, Instance).Select(r => r.Call));
    }

    [Fact]
    public void A_Call_Without_A_Row_Has_None()
    {
        var index = Build();

        Assert.Null(index.GetRow(Function, Instance, 3));
        Assert.Null(index.GetRow(Function, Instance + 8, 1));
    }

    [Fact]
    public void A_Call_Sees_The_Row_Its_Iterator_Returned_Last()
    {
        var index = Build();

        Assert.Equal(1, index.GetRowAt(Iterator, 101)?.Call);
        Assert.Equal(1, index.GetRowAt(Iterator, 200)?.Call);
        Assert.Equal(2, index.GetRowAt(Iterator, 201)?.Call);
    }

    [Fact]
    public void A_Call_Inside_The_First_GetRow_Sees_No_Row()
    {
        var index = Build();

        Assert.Null(index.GetRowAt(Iterator, 100));
        Assert.Null(index.GetRowAt(Iterator + 8, 500));
    }

    [Fact]
    public void Every_Function_Writing_An_Iterator_Buffer_Shares_One_Timeline()
    {
        var log = new RowBufferIndex.Log();

        Add(log, Function, call: 1, id: 100, number: 1, "First");

        Add(log, DirectSelect, call: 1, id: 300, number: 3, "Third");

        var row = log.Build().GetRowAt(Iterator, 301);

        Assert.Equal((DirectSelect, "Third"), (row?.Function, Encoding.ASCII.GetString(row!.Columns[1].Data)));
    }

    [Fact]
    public void Nodes_With_A_Row_Are_Annotated()
    {
        var tree = new CallStackTree();

        var scan = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);

        var other = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);

        var calls = new TraceCallStore.Builder();

        calls.Add(Function, Instance, Chunk(Row(1, node: 1), Row(2, node: 0), Row(3, node: 1)), 3);

        var store = calls.Build();

        store.MapNodes([other, scan]);

        var log = new RowBufferIndex.Log();

        Add(log, Function, call: 2, id: 100, number: 1, "First");

        log.Build().AnnotateNodes(store);

        Assert.Equal((true, false), (scan.HasRowBuffers, other.HasRowBuffers));
    }

    [Fact]
    public void A_Null_Column_Has_No_Data()
    {
        var log = new RowBufferIndex.Log();

        log.Add([new RawRow(Function, Instance, Iterator, Buffer, 100, 1, 0, 1)],
                [new RawColumn(0, 0, 0, 4, FixedBinding, 4, 3, 0)],
                []);

        var column = log.Build().GetRow(Function, Instance, 1)!.Columns[0];

        Assert.Equal((3, 0, "Status 3"), (column.Status, column.Data.Length, column.ToString()));
    }

    [Fact]
    public void A_Value_Row_Keeps_Its_Format_Through_Export()
    {
        var log = new RowBufferIndex.Log();

        log.Add([new RawRow(Function, Instance, Iterator, Buffer, 100, 1, 0, 1, RowBufferFormat.Values)],
                [new RawColumn(0, 8, 8, -1, 0, 0, 0, 0)],
                [0x20, 0x28, 0x30, 0, 0, 0, 0, 0]);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            log.Build().Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var row = RowBufferIndex.Read(reader).GetRow(Function, Instance, 1)!;

        Assert.Equal(RowBufferFormat.Values, row.Format);
        Assert.Equal([0x20, 0x28, 0x30, 0, 0, 0, 0, 0], row.Columns[0].Data);
    }

    [Fact]
    public void A_Value_On_A_Page_Is_Kept_As_A_Page_Reference()
    {
        var log = new RowBufferIndex.Log();

        var flags = (byte)(RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength | RowBufferColumnFlags.PageReference);

        byte[] reference = [0xA0, 0x01, 0x50, 0x01, 0, 0, 1, 0, 0x2A, 0, 0, 0, 0x10, 0, 0, 0, 0x03, 0];

        log.Add([new RawRow(Function, Instance, Iterator, Buffer, 100, 1, 0, 1)],
                [new RawColumn(0, 5, (uint)reference.Length, -1, VariableBinding, 8000, 0, flags)],
                reference);

        var column = log.Build().GetRow(Function, Instance, 1)!.Columns[0];

        Assert.Equal(new RowBufferPage(new PageAddress(1, 336), 416, new LogSequenceNumber(42, 16, 3)), column.Page);
        Assert.Empty(column.Data);
        Assert.Equal("Page (1:336) Offset 416", column.ToString());
    }

    [Fact]
    public void A_Value_Read_In_Part_Is_Unreadable_Not_Truncated()
    {
        var log = new RowBufferIndex.Log();

        var flags = (byte)(RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength | RowBufferColumnFlags.Unreadable);

        log.Add([new RawRow(Function, Instance, Iterator, Buffer, 100, 1, 0, 1)],
                [new RawColumn(0, 5, 2, -1, VariableBinding, 8000, 0, flags)],
                [0x46, 0x69]);

        Assert.Equal("0x4669 (Unreadable After 2 Bytes)", log.Build().GetRow(Function, Instance, 1)!.Columns[0].ToString());
    }

    [Fact]
    public void A_Row_Outside_Its_Chunk_Is_Skipped()
    {
        var log = new RowBufferIndex.Log();

        log.Add([new RawRow(Function, Instance, Iterator, Buffer, 100, 1, 1, 1)],
                [new RawColumn(0, 4, 4, 4, FixedBinding, 4, 0, 0)],
                [1, 0, 0, 0]);

        Assert.Equal(0, log.Build().Count);
    }

    [Fact]
    public void A_Row_Prints_Its_Columns_In_Hex()
    {
        var row = Build().GetRow(Function, Instance, 1)!;

        Assert.Equal($"Column 0  0x01000000{Environment.NewLine}Column 1  0x4669727374", row.ToString());
    }

    [Fact]
    public void Rows_Survive_Export_And_Import()
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            Build().Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var imported = RowBufferIndex.Read(reader);

        var row = imported.GetRow(Function, Instance, 2)!;

        Assert.Equal(2, imported.Count);
        Assert.Equal((200ul, Iterator, Buffer), (row.Id, row.Iterator, row.Address));
        Assert.Equal([2, 0, 0, 0], row.Columns[0].Data);
        Assert.Equal("Second", Encoding.ASCII.GetString(row.Columns[1].Data));
        Assert.Equal(RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength, row.Columns[1].Flags);
        Assert.Equal(2, imported.GetRowAt(Iterator, 201)?.Call);
    }

    private static (int, int, uint, bool) Describe(RowBufferColumn column)
        => (column.RecordOffset, column.FixedLength, column.BindingFlags, column.IsByReference);

    private static RowBufferIndex Build()
    {
        var log = new RowBufferIndex.Log();

        Add(log, Function, call: 2, id: 200, number: 2, "Second");

        Add(log, Function, call: 1, id: 100, number: 1, "First");

        return log.Build();
    }

    private static void Add(RowBufferIndex.Log log, ulong function, uint call, ulong id, byte number, string text)
    {
        var flags = (byte)(RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength);

        byte[] data = [number, 0, 0, 0, .. Encoding.ASCII.GetBytes(text)];

        log.Add([new RawRow(function, Instance, Iterator, Buffer, id, call, 0, 2)],
                [new RawColumn(0, 4, 4, 4, FixedBinding, 4, 0, 0),
                 new RawColumn(4, (uint)text.Length, (uint)text.Length, -1, VariableBinding, 8000, 0, flags)],
                data);
    }
}
