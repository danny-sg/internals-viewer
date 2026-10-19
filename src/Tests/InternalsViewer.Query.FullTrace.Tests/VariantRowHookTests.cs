using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantRows;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantRows.Enums;
using InternalsViewer.Query.FullTrace.RowSources;
using InternalsViewer.Query.FullTrace.RowSources.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class VariantRowHookTests
{
    private const uint Thread = 7;

    private const ulong GetRow = 0x1000;

    private const ulong CreateColumn = 0x2000;

    private const ulong HelperGetRow = 0x3000;

    private const ulong ColumnTable = 0x7000;

    private const ulong Aggregate = 0x1F4_0000_1000;

    private const ulong Context = 0x1F4_0000_2000;

    private const ulong KeyColumn = 0x1F4_0000_3000;

    private const ulong SumColumn = 0x1F4_0000_3100;

    private const ulong Row = 0x1F4_0000_4000;

    private const ulong Helper = 0x1F4_0000_5000;

    private const ulong HelperRow = 0x1F4_0000_6000;

    private const ulong Text = 0x1F4_0001_0068;

    private const ulong Batch = 0x1F4_0000_8000;

    private const ulong Vectors = 0x1F4_0000_9000;

    private const ulong DeepPage = 0x1F4_0000_A040;

    private const ulong DataPage = 0x1F4_0000_C000;

    private const int Slot = 1;

    private const ulong RowStatus = 0;

    private const ulong EndStatus = 0x40EC6;

    [Fact]
    public void A_Row_Returned_By_The_Iterator_That_Writes_A_Variant_Table_Captures_The_Whole_Table()
    {
        var row = Assert.Single(CaptureTable(RowStatus));

        Assert.Equal((RowBufferFormat.VariantTable, Row, Aggregate, 2), (row.Format, row.Address, row.Iterator, row.Columns.Count));
        Assert.Equal(BitConverter.GetBytes(42UL), row.Columns[1].Data);
    }

    [Fact]
    public void Nothing_Is_Captured_When_The_Iterator_Returns_The_End()
        => Assert.Empty(CaptureTable(EndStatus));

    [Fact]
    public void A_Batch_Helper_Row_Holds_Inline_Values_And_The_Bytes_Behind_Pointers()
    {
        var replay = HelperValues().Write(HelperRow + 0x20, (byte)0)
                                   .Write(HelperRow + 0x28, Text)
                                   .Write(HelperRow + 0x30, 6ul)
                                   .Write(Text, "SYM000"u8);

        var row = Assert.Single(CaptureHelper(replay));

        Assert.Equal((RowBufferFormat.Batch, HelperRow, Helper), (row.Format, row.Address, row.Iterator));
        Assert.Equal([0x20, 0x28, 0x30, 0, 0, 0, 0, 0], row.Columns[0].Data);
        Assert.Equal("SYM000", Encoding.ASCII.GetString(row.Columns[1].Data));
        Assert.True(row.Columns[1].IsByReference);
    }

    [Fact]
    public void Nothing_Is_Captured_When_The_Batch_Helper_Returns_The_End()
        => Assert.Empty(CaptureHelper(HelperValues(), EndStatus));

    [Fact]
    public void A_Batch_Helper_Row_Names_The_Vector_Whose_Deep_Data_Holds_A_Value()
    {
        var row = Assert.Single(CaptureHelper(DeepDataValues()));

        Assert.Equal((Batch, 2, 5), (row.Batch, row.BatchSelectionIndex, row.Columns[1].DeepDataVectorId));
        Assert.Equal(0, row.Columns[0].DeepDataVectorId);
    }

    [Fact]
    public void A_Page_Without_Its_Magic_Is_Not_Deep_Data()
    {
        var row = Assert.Single(CaptureHelper(DeepDataValues().Write(DeepPage + 0x10, 0u)));

        Assert.Equal(0, row.Columns[1].DeepDataVectorId);
    }

    [Fact]
    public void A_Null_Value_Keeps_Its_Status()
    {
        var column = Assert.Single(CaptureHelper(HelperValues().Write(HelperRow + 0x20, (byte)3))).Columns[1];

        Assert.Equal((3, 0), (column.Status, column.Data.Length));
    }

    [Fact]
    public void A_Value_Missing_From_The_Trace_Is_Unreadable()
    {
        var row = Assert.Single(CaptureHelper(HelperValues().Write(HelperRow + 0x20, (byte)0)));

        Assert.True(row.Columns[1].IsUnreadable);
    }

    [Fact]
    public void A_Batch_Helper_Row_Reference_Into_A_Data_Page_Is_Deferred()
    {
        var replay = HelperValues().Write(HelperRow + 0x20, (byte)0)
                                   .Write(HelperRow + 0x28, DataPage + 416)
                                   .Write(HelperRow + 0x30, 5ul)
                                   .Write(DataPage, (byte)1)
                                   .Write(DataPage + 0x20, (byte[])[0x50, 0x01, 0, 0, 1, 0])
                                   .Write(DataPage + 0x28, (byte[])[0x2A, 0, 0, 0, 0x10, 0, 0, 0, 0x03, 0]);

        var column = Assert.Single(CaptureHelper(replay)).Columns[1];

        Assert.Equal(new DeferredPageValue(new PageAddress(1, 336), 416, new LogSequenceNumber(42, 16, 3)), column.Deferred);
        Assert.Empty(column.Data);
    }

    [Fact]
    public void A_Batch_Helper_Whose_Row_Is_Missing_From_The_Trace_Uses_The_Row_Last_Read_For_It()
    {
        var hook = CreateHook(new RowSourceHook(RowSourceFunctions.Empty));

        ReturnHelperRow(hook, HelperValues(), 1, RowStatus);

        var second = new FakeTraceReplay().Write(HelperRow, (byte)0)
                                          .Write(HelperRow + 0x08, 7ul)
                                          .Write(HelperRow + 0x10, 0ul)
                                          .Write(HelperRow + 0x20, (byte)3);

        ReturnHelperRow(hook, second, 2, RowStatus);

        var row = hook.Rows[1];

        Assert.Equal((HelperRow, 2), (row.Address, row.Columns.Count));
        Assert.Equal(BitConverter.GetBytes(7UL), row.Columns[0].Data);
    }

    private static IReadOnlyList<RowBuffer> CaptureTable(ulong status)
    {
        var replay = new FakeTraceReplay().Write(KeyColumn, ColumnTable)
                                          .Write(KeyColumn + 0x08, Slot)
                                          .Write(KeyColumn + 0x0C, 0)
                                          .Write(SumColumn, ColumnTable)
                                          .Write(SumColumn + 0x08, Slot)
                                          .Write(SumColumn + 0x0C, 1)
                                          .Write(Context + Slot * sizeof(ulong), 0UL)
                                          .Write(Row, (byte)0)
                                          .Write(Row + 0x08, 7UL)
                                          .Write(Row + 0x10, 0UL)
                                          .Write(Row + 0x20, (byte)0)
                                          .Write(Row + 0x28, 42UL)
                                          .Write(Row + 0x30, 0UL);

        var rowSources = new RowSourceHook(new RowSourceFunctions(new Dictionary<ulong, RowSourceFunctionType>
                                                                  {
                                                                      [GetRow] = RowSourceFunctionType.Iterator
                                                                  }, RowSourceFunctions.Empty.PlanObjects));

        var hook = CreateHook(rowSources);

        hook.OnEntry(Call(CreateColumn, 1, [KeyColumn, Slot, 0]), replay);

        hook.OnReturn(Call(CreateColumn, 1, [KeyColumn, Slot, 0]), replay);

        hook.OnEntry(Call(CreateColumn, 2, [SumColumn, Slot, 0]), replay);

        hook.OnReturn(Call(CreateColumn, 2, [SumColumn, Slot, 0]), replay);

        rowSources.OnEntry(Call(GetRow, 3, [Aggregate, Context, 0]), replay);

        hook.OnEntry(Call(GetRow, 3, [Aggregate, Context, 0]), replay);

        replay.Write(Context + Slot * sizeof(ulong), Row);

        rowSources.OnReturn(Call(GetRow, 3, [Aggregate, Context, 0], returnValue: status), replay);

        hook.OnReturn(Call(GetRow, 3, [Aggregate, Context, 0], returnValue: status), replay);

        return hook.Rows;
    }

    private static IReadOnlyList<RowBuffer> CaptureHelper(FakeTraceReplay replay, ulong status = RowStatus)
    {
        var hook = CreateHook(new RowSourceHook(RowSourceFunctions.Empty));

        ReturnHelperRow(hook, replay, 1, status);

        return hook.Rows;
    }

    private static void ReturnHelperRow(VariantRowHook hook, FakeTraceReplay replay, ulong id, ulong status)
    {
        hook.OnEntry(Call(HelperGetRow, id, [Helper, Context]), replay);

        hook.OnReturn(Call(HelperGetRow, id, [Helper, Context], returnValue: status), replay);
    }

    private static FakeTraceReplay HelperValues()
        => new FakeTraceReplay().Write(Helper + 0x78, HelperRow)
                                .Write(Helper + 0x50, 2u)
                                .Write(HelperRow, (byte)0)
                                .Write(HelperRow + 0x08, 3156000ul)
                                .Write(HelperRow + 0x10, 0ul);

    private static FakeTraceReplay DeepDataValues()
        => HelperValues().Write(HelperRow + 0x20, (byte)0)
                         .Write(HelperRow + 0x28, DeepPage + 0x1C)
                         .Write(HelperRow + 0x30, 6ul)
                         .Write(DeepPage + 0x1C, "SYM000"u8)
                         .Write(Helper + 0x88, 3)
                         .Write(Helper + 0x90, Batch)
                         .Write(Batch + 0x08, Vectors)
                         .Write(Batch + 0x1C, 6)
                         .Write(Vectors + 4 * 0x58 + 0x18, DeepPage)
                         .Write(DeepPage + 0x08, 0ul)
                         .Write(DeepPage + 0x10, 0x53445042u)
                         .Write(DeepPage + 0x14, (ushort)0x1F8C);

    private static VariantRowHook CreateHook(RowSourceHook rowSources)
        => new(new VariantRowFunctions(new Dictionary<ulong, VariantRowFunctionType>
                                       {
                                           [GetRow] = VariantRowFunctionType.Iterator,
                                           [HelperGetRow] = VariantRowFunctionType.BatchHelper,
                                           [CreateColumn] = VariantRowFunctionType.ColumnCreator
                                       },
                                       new Dictionary<ulong, VariantTableClass>
                                       {
                                           [ColumnTable] = VariantTableClass.Column
                                       }),
               rowSources);

    private static TraceHookCall Call(ulong function, ulong id, ulong[] arguments, ulong returnValue = 0)
        => new()
        {
            Function = function,
            Instance = function switch
            {
                GetRow => Aggregate,
                HelperGetRow => Helper,
                _ => 0
            },
            Id = id,
            Call = (uint)id,
            Position = At(id),
            Thread = Thread,
            Arguments = arguments,
            ReturnValue = returnValue
        };
}
