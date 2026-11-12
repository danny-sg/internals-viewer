using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators.PlanNodes.Output;

[Trait("Category", "Unit")]
public class PlanOutputValueReaderTests
{
    private const ulong Entry = 0x1F4_0000_1000;

    private const ulong Data = 0x1F4_0000_2000;

    private const ulong RowSlot = 0x1F4_0000_3000;

    private const ulong Row = 0x1F4_0000_4000;

    private const ulong Frame = 0x1F4_0001_0000;

    private static readonly SqlTypeInfo IntType = new(56, 0, 4, 10, 0, 0);

    private static readonly SqlTypeInfo VarcharType = new(167, 0, 100, 0, 0, 0);

    private static readonly SqlTypeInfo CharType = new(175, 0, 10, 0, 0, 0);

    [Fact]
    public void A_Fixed_Buffer_Entry_Is_Read_Inline()
    {
        var memory = new FakeTraceReplay().Write(Entry, (byte)0).Write(Entry + 0x08, 42);

        Assert.Equal("42", PlanOutputValueReader.Read(memory, Column(IntType), Path(PlanOutputLinkType.BufferEntry, Entry, [])));
    }

    [Fact]
    public void A_Variable_Buffer_Entry_Is_Read_Through_Its_Pointer()
    {
        var memory = new FakeTraceReplay().Write(Entry, (byte)0)
                                          .Write(Entry + 0x08, Data)
                                          .Write(Entry + 0x10, 3UL)
                                          .Write(Data, (byte)'a')
                                          .Write(Data + 1, (byte)'b')
                                          .Write(Data + 2, (byte)'c');

        Assert.Equal("abc", PlanOutputValueReader.Read(memory, Column(VarcharType), Path(PlanOutputLinkType.BufferEntry, Entry, [])));
    }

    [Fact]
    public void A_Null_Status_Reads_As_Null()
    {
        var memory = new FakeTraceReplay().Write(Entry, (byte)3);

        Assert.Equal("NULL", PlanOutputValueReader.Read(memory, Column(IntType), Path(PlanOutputLinkType.BufferEntry, Entry, [])));
    }

    [Fact]
    public void A_Hash_Column_Is_Read_From_The_Row_In_Its_Row_Slot()
    {
        var memory = new FakeTraceReplay().Write(RowSlot, Row).Write(Row + 4, 7);

        var value = PlanOutputValueReader.Read(memory, Column(IntType), Path(PlanOutputLinkType.RowSlot, RowSlot, [0], 4));

        Assert.Equal("7", value);
    }

    [Fact]
    public void A_Fixed_Char_Hash_Column_Is_Read_From_The_Row()
    {
        var memory = new FakeTraceReplay().Write(RowSlot, Row).Write(Row + 4, "Row 200   "u8);

        var value = PlanOutputValueReader.Read(memory, Column(CharType), Path(PlanOutputLinkType.RowSlot, RowSlot, [0], 4));

        Assert.Equal("Row 200   ", value);
    }

    [Fact]
    public void A_Variable_Hash_Column_Is_Read_From_The_Variable_Part_Of_The_Row()
    {
        byte[] row = [0x30, 0x00, 0x0C, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x01, 0x00, 0x18, 0x00,
                      .. "Row 1"u8];

        var memory = new FakeTraceReplay().Write(RowSlot, Row).Write(Row, row);

        var value = PlanOutputValueReader.Read(memory, Column(VarcharType), Path(PlanOutputLinkType.RowSlot, RowSlot, [0], -1));

        Assert.Equal("Row 1", value);
    }

    [Fact]
    public void A_Column_Without_A_Readable_Source_Has_No_Value()
    {
        var value = PlanOutputValueReader.Read(new FakeTraceReplay(), Column(IntType), PlanOutputPath.Empty);

        Assert.Null(value);
    }

    [Fact]
    public void A_Record_Source_Locates_The_Current_Record_On_Its_Page()
    {
        var builder = new TraceBufferPool.Builder();

        builder.Add(new TraceBufferPage(Frame, new PageAddress(1, 2728), 6, At(1)));

        var memory = new FakeTraceReplay().Write(RowSlot, Frame + 0x81);

        var record = PlanOutputValueReader.ReadRecord(memory,
                                                      builder.Build(),
                                                      At(5),
                                                      new PlanOutputPath
                                                      {
                                                          Record = new PlanOutputValueSource(PlanOutputLinkType.Record, RowSlot, [0], -1)
                                                      });

        Assert.Equal(new PlanOutputRecord(new TraceBufferReference(new PageAddress(1, 2728), 6, 0x81), -1), record);
    }

    private static PlanOutputColumn Column(SqlTypeInfo type) => new(Data, 0, type);

    private static PlanOutputPath Path(PlanOutputLinkType type, ulong address, ulong[] chain, int? offset = null)
        => new() { Value = new PlanOutputValueSource(type, address, chain, offset) };
}
