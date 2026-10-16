using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Batches.Enums;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class RowBufferStoreTests
{
    private const ulong Function = 0x7FF8_0010_0000;

    private const ulong DirectSelect = 0x7FF8_0010_0400;

    private const ulong Instance = 0x1F4_EEAE_1820;

    private const ulong Iterator = Instance;

    private const ulong Buffer = 0x1F4_EEAE_2000;

    private const uint FixedBinding = 0x2;

    private const uint VariableBinding = 0x902;

    private static readonly RowBufferBinding Fixed = new(4, FixedBinding, 4, 0x00);

    private static readonly RowBufferBinding Variable = new(-1, VariableBinding, 8000, 0x08);

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

        Assert.Equal((Fixed, false), (columns[0].Binding, columns[0].IsByReference));
        Assert.Equal((Variable, true), (columns[1].Binding, columns[1].IsByReference));
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
        var index = new RowBufferStore([CreateRow(Function, call: 1, id: 100, number: 1, "First"),
                                        CreateRow(DirectSelect, call: 1, id: 300, number: 3, "Third")]);

        var row = index.GetRowAt(Iterator, 301);

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

        new RowBufferStore([CreateRow(Function, call: 2, id: 100, number: 1, "First")]).AnnotateNodes(store);

        Assert.Equal((true, false), (scan.HasRowBuffers, other.HasRowBuffers));
    }

    [Fact]
    public void A_Null_Column_Shows_Its_Status()
    {
        var column = new RowBufferColumn(0, Fixed, 3, RowBufferColumnFlags.None, 0, []);

        Assert.Equal("Status 3", column.ToString());
    }

    [Fact]
    public void A_Value_Row_Keeps_Its_Format_Through_Export()
    {
        var column = new RowBufferColumn(0, RowBufferBinding.None, 0, RowBufferColumnFlags.None, 8, [0x20, 0x28, 0x30, 0, 0, 0, 0, 0]);

        var index = new RowBufferStore([new RowBuffer(Function, Instance, 1, 100, Iterator, Buffer, [column], RowBufferFormat.Values)]);

        var row = Export(index).GetRow(Function, Instance, 1)!;

        Assert.Equal(RowBufferFormat.Values, row.Format);
        Assert.Equal([0x20, 0x28, 0x30, 0, 0, 0, 0, 0], row.Columns[0].Data);
    }

    [Fact]
    public void A_Page_Reference_Survives_Export_And_Import()
    {
        var flags = RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength | RowBufferColumnFlags.Deferred;

        var page = new DeferredPageValue(new PageAddress(1, 336), 416, new LogSequenceNumber(42, 16, 3));

        var column = new RowBufferColumn(0, Variable, 0, flags, 5, []) { Deferred = page };

        column = Export(new RowBufferStore([new RowBuffer(Function, Instance, 1, 100, Iterator, Buffer, [column])]))
            .GetRow(Function, Instance, 1)!.Columns[0];

        Assert.Equal(page, column.Deferred);
        Assert.Empty(column.Data);
        Assert.Equal("Page (1:336) Offset 416", column.ToString());
    }

    [Fact]
    public void A_Deferred_Lob_Survives_Export_And_Import()
    {
        var lob = new DeferredLobValue([new DeferredLobLink(8040, new PageAddress(1, 39873), 0),
                                   new DeferredLobLink(20000, new PageAddress(1, 39872), 2)]);

        var flags = RowBufferColumnFlags.HasLength | RowBufferColumnFlags.Deferred;

        var column = new RowBufferColumn(0, Variable, 0, flags, 100, []) { Deferred = lob };

        var row = Export(new RowBufferStore([new RowBuffer(Function, Instance, 1, 100, Iterator, Buffer, [column])]))
            .GetRow(Function, Instance, 1)!;

        var imported = Assert.IsType<DeferredLobValue>(row.Columns[0].Deferred);

        Assert.Equal(lob.Links, imported.Links);
    }

    [Fact]
    public void A_Value_Read_In_Part_Is_Unreadable_Not_Truncated()
    {
        var flags = RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength | RowBufferColumnFlags.Unreadable;

        var column = new RowBufferColumn(0, Variable, 0, flags, 5, [0x46, 0x69]);

        Assert.Equal("0x4669 (Unreadable After 2 Bytes)", column.ToString());
    }

    [Fact]
    public void Rows_Sharing_Columns_Still_Share_Them_After_Export_And_Import()
    {
        var first = CreateRow(Function, call: 1, id: 100, number: 1, "First");

        var imported = Export(new RowBufferStore([first, first with { Call = 2, Id = 200 }]));

        var (left, right) = (imported.GetRow(Function, Instance, 1)!, imported.GetRow(Function, Instance, 2)!);

        Assert.Same(left.Columns, right.Columns);
        Assert.Same(left.Columns[0].Binding, right.Columns[0].Binding);
    }

    [Fact]
    public void A_Value_Is_Sourced_From_Its_Binding_A_Page_Or_Memory()
    {
        var row = CreateRow(Function, call: 1, id: 100, number: 1, "First");

        var index = new RowBufferStore([row]);

        var page = row.Columns[1] with
        {
            Pointer = 0x2_0000_01A0,
            Deferred = new DeferredPageValue(new PageAddress(1, 336), 416, new LogSequenceNumber(42, 16, 3))
        };

        var memory = row.Columns[1] with { Pointer = 0x3_0000_0000 };

        Assert.Equal([RowBufferSourceType.Binding, RowBufferSourceType.BufferPool, RowBufferSourceType.Memory],
                     [index.GetSource(row, row.Columns[0]).Type, index.GetSource(row, page).Type, index.GetSource(row, memory).Type]);
    }

    [Fact]
    public void A_Value_Pointing_Into_Another_Row_Buffer_Names_It_And_The_Offset()
    {
        var other = CreateRow(Function, call: 1, id: 100, number: 1, "First");

        var pointer = Buffer + 0x0A;

        var reference = new RowBufferColumn(0, Variable, 0, RowBufferColumnFlags.ByReference, 3, [0x72, 0x73, 0x74]) { Pointer = pointer };

        var row = new RowBuffer(DirectSelect, Instance + 8, 1, 300, Iterator + 8, 0x1F4_EEAF_0000, [reference]);

        var index = new RowBufferStore([other, row]);

        Assert.Equal(new RowBufferSource(RowBufferSourceType.RowBuffer, pointer, Iterator, 0x0A, 1), index.GetSource(row, reference));
    }

    [Fact]
    public void A_Value_Pointing_At_A_Batch_Value_Names_Its_Vector_And_Row()
    {
        const ulong deep = 0x2_0000_0400;

        const ulong batchIterator = 0x2_0000_9000;

        var vector = new BatchCaptureVector(7,
                                            BatchVectorFlags.None,
                                            [1, deep | 1],
                                            new Dictionary<int, BatchDeepValue> { [1] = new(5, "Fifth"u8.ToArray()) });

        var capture = new BatchCapture(Function, 0, 0, 10, batchIterator, 0x2_0000_8000, 0, BatchCaptureFlags.None, [0, 1], [vector]);

        var batches = new BatchStore([capture]);

        var row = CreateRow(Function, call: 1, id: 100, number: 1, "First");

        var value = row.Columns[1] with { Pointer = deep };

        Assert.Equal(new RowBufferSource(RowBufferSourceType.Batch, deep, batchIterator, 0, 7, 1),
                     new RowBufferStore([row]).GetSource(row, value, batches));
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
        var imported = Export(Build());

        var row = imported.GetRow(Function, Instance, 2)!;

        Assert.Equal(2, imported.Count);
        Assert.Equal((200ul, Iterator, Buffer), (row.Id, row.Iterator, row.Address));
        Assert.Equal([2, 0, 0, 0], row.Columns[0].Data);
        Assert.Equal("Second", Encoding.ASCII.GetString(row.Columns[1].Data));
        Assert.Equal(RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength, row.Columns[1].Flags);
        Assert.Equal(2, imported.GetRowAt(Iterator, 201)?.Call);
    }

    private static RowBufferStore Build()
        => new([CreateRow(Function, call: 2, id: 200, number: 2, "Second"), CreateRow(Function, call: 1, id: 100, number: 1, "First")]);

    private static RowBuffer CreateRow(ulong function, int call, ulong id, byte number, string text)
    {
        var flags = RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength;

        return new RowBuffer(function,
                             Instance,
                             call,
                             id,
                             Iterator,
                             Buffer,
                             [new RowBufferColumn(0, Fixed, 0, RowBufferColumnFlags.None, 4, [number, 0, 0, 0]),
                              new RowBufferColumn(1, Variable, 0, flags, text.Length, Encoding.ASCII.GetBytes(text))]);
    }

    private static RowBufferStore Export(RowBufferStore index)
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            index.Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        return RowBufferStore.Read(reader);
    }
}
