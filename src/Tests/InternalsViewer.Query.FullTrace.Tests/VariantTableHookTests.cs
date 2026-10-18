using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantTables;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantTables.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class VariantTableHookTests
{
    private const uint Thread = 7;

    private const ulong GetRow = 0x1000;

    private const ulong CreateColumn = 0x2000;

    private const ulong Write = 0x3000;

    private const ulong ColumnTable = 0x7000;

    private const ulong Aggregate = 0x1F4_0000_1000;

    private const ulong Context = 0x1F4_0000_2000;

    private const ulong KeyColumn = 0x1F4_0000_3000;

    private const ulong SumColumn = 0x1F4_0000_3100;

    private const ulong Row = 0x1F4_0000_4000;

    private const int Slot = 1;

    private const ulong RowStatus = 0;

    private const ulong EndStatus = 0x40EC6;

    [Fact]
    public void A_Row_Returned_By_The_Iterator_That_Writes_A_Variant_Table_Captures_The_Whole_Table()
    {
        var row = Assert.Single(Capture(RowStatus));

        Assert.Equal((RowBufferFormat.VariantTable, Row, Aggregate, 2), (row.Format, row.Address, row.Iterator, row.Columns.Count));
        Assert.Equal(BitConverter.GetBytes(42UL), row.Columns[1].Data);
    }

    [Fact]
    public void Nothing_Is_Captured_When_The_Iterator_Returns_The_End()
        => Assert.Empty(Capture(EndStatus));

    private static IReadOnlyList<RowBuffer> Capture(ulong status)
    {
        var replay = new FakeTraceReplay().Write(KeyColumn, ColumnTable)
                                          .Write(KeyColumn + 0x08, Slot)
                                          .Write(KeyColumn + 0x0C, 0)
                                          .Write(SumColumn, ColumnTable)
                                          .Write(SumColumn + 0x08, Slot)
                                          .Write(SumColumn + 0x0C, 1)
                                          .Write(Context + Slot * sizeof(ulong), Row)
                                          .Write(Row, (byte)0)
                                          .Write(Row + 0x08, 7UL)
                                          .Write(Row + 0x10, 0UL)
                                          .Write(Row + 0x20, (byte)0)
                                          .Write(Row + 0x28, 42UL)
                                          .Write(Row + 0x30, 0UL);

        var hook = new VariantTableHook(new VariantTableFunctions(new Dictionary<ulong, VariantTableFunctionType>
                                                                  {
                                                                      [GetRow] = VariantTableFunctionType.Iterator,
                                                                      [CreateColumn] = VariantTableFunctionType.ColumnCreator,
                                                                      [Write] = VariantTableFunctionType.Writer
                                                                  },
                                                                  new Dictionary<ulong, VariantTableClass>
                                                                  {
                                                                      [ColumnTable] = VariantTableClass.Column
                                                                  }));

        hook.OnEntry(Call(CreateColumn, 1, [KeyColumn, Slot, 0]), replay);

        hook.OnReturn(Call(CreateColumn, 1, [KeyColumn, Slot, 0]), replay);

        hook.OnEntry(Call(CreateColumn, 2, [SumColumn, Slot, 0]), replay);

        hook.OnReturn(Call(CreateColumn, 2, [SumColumn, Slot, 0]), replay);

        hook.OnEntry(Call(GetRow, 3, [Aggregate, 0, 0]), replay);

        hook.OnEntry(Call(Write, 4, [KeyColumn, Context, 0]), replay);

        hook.OnReturn(Call(Write, 4, [KeyColumn, Context, 0]), replay);

        hook.OnReturn(Call(GetRow, 3, [Aggregate, 0, 0], returnValue: status), replay);

        return hook.Rows;
    }

    private static TraceHookCall Call(ulong function, ulong id, ulong[] arguments, ulong returnValue = 0)
        => new()
        {
            Function = function,
            Instance = function == GetRow ? Aggregate : 0,
            Id = id,
            Call = (uint)id,
            Position = At(id),
            Thread = Thread,
            Arguments = arguments,
            ReturnValue = returnValue
        };
}
