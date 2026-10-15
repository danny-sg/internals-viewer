using System.Text;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class RowBufferHookTests
{
    private const ulong Function = 0x7FF8_0030_0000;

    private const ulong Iterator = 0x21C_0BB4_C180;

    private const ulong ValueTable = 0x7FF8_0040_0000;

    private const ulong BindingTable = 0x7FF8_0041_0000;

    private const ulong Row = 0x21C_0BB4_C620;

    private const ulong Text = 0x21C_1EB7_C068;

    private const ulong Buffer = 0x21C_0BB5_0000;

    private const ulong Bindings = 0x21C_0BB6_0000;

    private const ulong BindingArray = 0x21C_0BB7_0000;

    private const ulong BindingInfo = 0x21C_0BB8_0000;

    [Fact]
    public void A_Value_Row_Holds_Inline_Values_And_The_Bytes_Behind_Pointers()
    {
        var memory = ValueRow().Write(Row + 0x20, (byte)0)
                               .Write(Row + 0x28, Text)
                               .Write(Row + 0x30, 6ul)
                               .Write(Text, "SYM000"u8);

        var row = Replay(memory, count: 2)!;

        Assert.Equal(RowBufferFormat.Values, row.Format);
        Assert.Equal([0x20, 0x28, 0x30, 0, 0, 0, 0, 0], row.Columns[0].Data);
        Assert.Equal("SYM000", Encoding.ASCII.GetString(row.Columns[1].Data));
        Assert.True(row.Columns[1].IsByReference);
    }

    [Fact]
    public void A_Null_Value_Keeps_Its_Status()
    {
        var memory = ValueRow().Write(Row + 0x20, (byte)3);

        var column = Replay(memory, count: 2)!.Columns[1];

        Assert.Equal((3, 0), (column.Status, column.Data.Length));
    }

    [Fact]
    public void A_Value_Missing_From_The_Trace_Is_Unreadable()
    {
        var memory = ValueRow().Write(Row + 0x20, (byte)0);

        Assert.True(Replay(memory, count: 2)!.Columns[1].IsUnreadable);
    }

    [Fact]
    public void A_Bound_Row_Reads_Each_Column_At_Its_Binding()
    {
        var memory = new FakeTraceMemory().Write(Iterator, BindingTable)
                                          .Write(Iterator + 0x80, Buffer)
                                          .Write(Iterator + 0x88, Bindings)
                                          .Write(Bindings + 0x62, (ushort)1)
                                          .Write(Bindings + 0x70, BindingArray)
                                          .Write(BindingArray, 4)
                                          .Write(BindingArray + 0x28, 0x10ul)
                                          .Write(BindingArray + 0x30, 0x08ul)
                                          .Write(BindingArray + 0x3C, (ushort)4)
                                          .Write(BindingArray + 0x40, 2u)
                                          .Write(BindingArray + 0x48, BindingInfo)
                                          .Write(BindingInfo + 0x18, 0ul)
                                          .Write(BindingInfo + 0x2A, (byte)0)
                                          .Write(Buffer + 0x08, (byte)0)
                                          .Write(Buffer + 0x10, 3156000);

        var row = Replay(memory)!;

        Assert.Equal(RowBufferFormat.Bindings, row.Format);
        Assert.Equal((4, 4), (row.Columns[0].RecordOffset, row.Columns[0].FixedLength));
        Assert.Equal([0x20, 0x28, 0x30, 0], row.Columns[0].Data);
    }

    [Fact]
    public void An_Unknown_Class_Has_No_Row()
    {
        var memory = new FakeTraceMemory().Write(Iterator, 0x7FF8_0042_0000ul);

        Assert.Null(Replay(memory));
    }

    [Fact]
    public void A_Call_Left_Without_Returning_Has_No_Row()
    {
        var log = new RowBufferIndex.Log();

        var hook = CreateHook(log);

        var memory = ValueRow();

        hook.OnEntry(Call(), memory);

        hook.OnAbandoned(Call());

        hook.OnReturn(Call(), memory);

        Assert.Equal(0, log.Build().Count);
    }

    private static FakeTraceMemory ValueRow()
        => new FakeTraceMemory().Write(Iterator, ValueTable)
                                .Write(Iterator + 0x78, Row)
                                .Write(Iterator + 0x50, 2u)
                                .Write(Row, (byte)0)
                                .Write(Row + 0x08, 3156000ul)
                                .Write(Row + 0x10, 0ul);

    private static RowBuffer? Replay(FakeTraceMemory memory, uint count = 1)
    {
        var log = new RowBufferIndex.Log();

        var hook = CreateHook(log);

        memory.Write(Iterator + 0x50, count);

        hook.OnEntry(Call(), memory);

        hook.OnReturn(Call(), memory);

        return log.Build().GetRow(Function, Iterator, 3);
    }

    private static RowBufferHook CreateHook(RowBufferIndex.Log log)
        => new(new RowBufferFunctions([new RowBufferRead(Function, 0)],
                                      [new RowBufferLayout(ValueTable, 0x78, 0, 0x50, RowBufferFormat.Values),
                                       new RowBufferLayout(BindingTable, 0x80, 0x88)]),
               log);

    private static TraceHookCall Call()
        => new()
        {
            Function = Function,
            Instance = Iterator,
            Id = 100,
            CallCount = 120,
            Call = 3,
            Arguments = new[] { Iterator, 0ul, 0ul, 0ul }
        };
}
