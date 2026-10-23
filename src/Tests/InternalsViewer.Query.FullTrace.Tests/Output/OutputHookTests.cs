using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Output.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Output;

[Trait("Category", "Unit")]
public class OutputHookTests
{
    private const uint Thread = 7;

    private const ulong Evaluate = 0x1000;

    private const ulong Read = 0x2000;

    private const ulong VariantRead = 0x2100;

    private const ulong Send = 0x2200;

    private const ulong OutputRowTable = 0x7000;

    private const ulong SwitchTable = 0x7100;

    private const ulong HashTable = 0x7200;

    private const ulong ScanTable = 0x7300;

    private const ulong OtherTable = 0x7400;

    private const ulong RedirectTable = 0x7500;

    private const ulong VariantTable = 0x7600;

    private const ulong Executor = 0x1F4_0000_1000;

    private const ulong Instruction = 0x1F4_0000_2000;

    private const ulong Values = 0x1F4_0000_3000;

    private const ulong Variants = 0x1F4_0000_3800;

    private const ulong Context = 0x1F4_0000_4000;

    private const ulong OutputRowValue = 0x1F4_0000_5000;

    private const ulong SwitchValue = 0x1F4_0000_5100;

    private const ulong HashValue = 0x1F4_0000_5200;

    private const ulong ScanValue = 0x1F4_0000_5300;

    private const ulong Rowset = 0x1F4_0000_6000;

    private const ulong HashIterator = 0x1F4_0000_7000;

    private const ulong SortBuffer = 0x1F4_0000_B000;

    private const ulong BatchHelper = 0x1F4_0000_C000;

    private const ulong RedirectValue = 0x1F4_0000_5400;

    private const ulong VariantValue = 0x1F4_0000_5500;

    private const ulong VariantRow = 0x1F4_0000_D000;

    private const int VariantSlot = 5;

    private const ulong Record = 0x1F4_0001_0000;

    private const ulong Variant = 0x1F4_0000_9000;

    private const int Slot = 2;

    [Fact]
    public void A_Column_Read_Through_A_Hash_Switch_Points_Where_Its_Primary_Value_Reads()
        => Assert.Equal(Record, Assert.Single(Capture(CreateReplay())).Columns[0]);

    [Fact]
    public void A_Column_Read_From_A_Table_Points_At_Its_Rowset()
        => Assert.Equal(Rowset, Assert.Single(Capture(CreateReplay())).Columns[1]);

    [Fact]
    public void A_Column_Read_From_A_Hash_Record_Points_At_The_Record_In_Its_Slot()
    {
        var replay = CreateReplay().Write(Values + 0x08, HashValue);

        Assert.Equal(Record, Assert.Single(Capture(replay)).Columns[0]);
    }

    [Fact]
    public void The_Structure_Is_Placed_At_The_Evaluation_It_Was_Learned_From()
        => Assert.Equal(At(10), Assert.Single(Capture(CreateReplay())).Position);

    [Fact]
    public void The_Structure_Belongs_To_The_Output_Row_It_Was_Read_For()
        => Assert.Equal(OutputRowValue, Assert.Single(Capture(CreateReplay())).OutputRow);

    [Fact]
    public void The_Structure_Is_Learned_From_The_First_Output_Row()
    {
        var hook = CreateHook();

        Evaluation(hook, CreateReplay(), 10);

        Evaluation(hook, CreateReplay().Write(Values + 0x10, SwitchValue), 20);

        Assert.Equal(Rowset, Assert.Single(hook.Build()).Columns[1]);
    }

    [Fact]
    public void A_Row_Sent_Directly_Points_At_The_Values_It_Was_Sent_From()
    {
        var hook = CreateHook();

        hook.OnEntry(Call(Send, 30, [OutputRowValue, Context, SortBuffer]), CreateReplay());

        var structure = Assert.Single(hook.Build());

        Assert.Equal((OutputRowValue, SortBuffer, 0), (structure.OutputRow, structure.DirectSelect, structure.Columns.Count));
    }

    [Fact]
    public void A_Row_Sent_From_The_Output_Evaluation_Is_Not_Sent_Directly()
    {
        var replay = CreateReplay();

        var hook = CreateHook();

        hook.OnEntry(Call(Evaluate, 10, [Context, Executor]), replay);

        ReadColumn(hook, replay, 11, 1);

        hook.OnEntry(Call(Send, 12, [OutputRowValue, Context, SortBuffer]), replay);

        hook.OnReturn(Call(Evaluate, 10, [Context, Executor]), replay);

        Assert.Equal(0UL, Assert.Single(hook.Build()).DirectSelect);
    }

    [Fact]
    public void A_Column_Read_From_A_Variant_Points_At_The_Variant()
    {
        var replay = CreateReplay().Write(Variants + 0x08, SortBuffer + 0x20);

        var hook = CreateHook();

        hook.OnEntry(Call(Evaluate, 10, [Context, Executor]), replay);

        ReadColumn(hook, replay, 11, 1, function: VariantRead);

        hook.OnReturn(Call(Evaluate, 10, [Context, Executor]), replay);

        Assert.Equal(SortBuffer + 0x20, Assert.Single(Assert.Single(hook.Build()).Columns));
    }

    [Fact]
    public void A_Column_Redirected_To_A_Batch_Row_Points_At_The_Batch_Helper()
    {
        var replay = CreateReplay().Write(Values + 0x08, RedirectValue)
                                   .Write(RedirectValue, RedirectTable)
                                   .Write(RedirectValue + 0x08, 1)
                                   .Write(RedirectValue + 0x10, BatchHelper);

        var hook = CreateHook();

        hook.OnEntry(Call(Evaluate, 10, [Context, Executor]), replay);

        ReadColumn(hook, replay, 11, 1);

        hook.OnReturn(Call(Evaluate, 10, [Context, Executor]), replay);

        Assert.Equal(BatchHelper, Assert.Single(Assert.Single(hook.Build()).Columns));
    }

    [Fact]
    public void A_Column_Read_From_A_Variant_Table_Points_At_Its_Variant_In_The_Row_In_Its_Slot()
    {
        var replay = CreateReplay().Write(Values + 0x08, VariantValue)
                                   .Write(VariantValue, VariantTable)
                                   .Write(VariantValue + 0x08, VariantSlot)
                                   .Write(VariantValue + 0x0C, 1)
                                   .Write(Context + VariantSlot * sizeof(ulong), VariantRow);

        var hook = CreateHook();

        hook.OnEntry(Call(Evaluate, 10, [Context, Executor]), replay);

        ReadColumn(hook, replay, 11, 1);

        hook.OnReturn(Call(Evaluate, 10, [Context, Executor]), replay);

        Assert.Equal(VariantRow + 0x20, Assert.Single(Assert.Single(hook.Build()).Columns));
    }

    [Fact]
    public void An_Evaluation_Of_Anything_But_The_Output_Row_Is_Ignored()
        => Assert.Empty(Capture(CreateReplay().Write(OutputRowValue, OtherTable)));

    [Fact]
    public void An_Evaluation_Whose_Type_Cannot_Be_Read_Is_Kept_Once_A_Later_One_Shows_It_Is_The_Output_Row()
    {
        var hook = CreateHook();

        Evaluation(hook, CreateReplay().Write(Values, 0UL), 10);

        Evaluation(hook, CreateReplay(), 20);

        Assert.Equal(2, Assert.Single(hook.Build()).Columns.Count);
    }

    [Fact]
    public void A_Read_Inside_A_Nested_Evaluation_Is_Not_A_Column_Of_The_Output_Row()
    {
        var replay = CreateReplay();

        var hook = CreateHook();

        hook.OnEntry(Call(Evaluate, 10, [Context, Executor]), replay);

        hook.OnEntry(Call(Evaluate, 11, [Context, Executor + 0x100]), replay);

        ReadColumn(hook, replay, 12, 1, Executor + 0x100);

        hook.OnReturn(Call(Evaluate, 11, [Context, Executor + 0x100]), replay);

        ReadColumn(hook, replay, 13, 1);

        hook.OnReturn(Call(Evaluate, 10, [Context, Executor]), replay);

        Assert.Single(Assert.Single(hook.Build()).Columns);
    }

    [Fact]
    public void Functions_Without_The_Output_Row_Class_Hook_Nothing()
        => Assert.Empty(new OutputHook(new OutputFunctions(CreateFunctions(), new Dictionary<ulong, OutputValueClass>())).Functions);

    private static IReadOnlyList<OutputAddresses> Capture(FakeTraceReplay replay)
    {
        var hook = CreateHook();

        Evaluation(hook, replay, 10);

        return hook.Build();
    }

    private static OutputHook CreateHook()
        => new(new OutputFunctions(CreateFunctions(),
                                   new Dictionary<ulong, OutputValueClass>
                                   {
                                       [OutputRowTable] = OutputValueClass.OutputRow,
                                       [SwitchTable] = OutputValueClass.HashSwitch,
                                       [HashTable] = OutputValueClass.Hash,
                                       [ScanTable] = OutputValueClass.Table,
                                       [RedirectTable] = OutputValueClass.BatchRedirect,
                                       [VariantTable] = OutputValueClass.VariantTable
                                   }));

    private static Dictionary<ulong, OutputFunctionType> CreateFunctions()
        => new()
        {
            [Evaluate] = OutputFunctionType.Evaluation,
            [Read] = OutputFunctionType.ValueRead,
            [VariantRead] = OutputFunctionType.VariantRead,
            [Send] = OutputFunctionType.Send
        };

    private static void Evaluation(OutputHook hook, FakeTraceReplay replay, ulong id)
    {
        hook.OnEntry(Call(Evaluate, id, [Context, Executor]), replay);

        ReadColumn(hook, replay, id + 1, 1);

        ReadColumn(hook, replay, id + 2, 2);

        hook.OnReturn(Call(Evaluate, id, [Context, Executor]), replay);
    }

    private static void ReadColumn(OutputHook hook,
                                   FakeTraceReplay replay,
                                   ulong id,
                                   uint index,
                                   ulong executor = Executor,
                                   ulong function = Read)
    {
        replay.Write(Instruction + 0x0C, index);

        hook.OnEntry(Call(function, id, [executor, Variant]), replay);

        hook.OnReturn(Call(function, id, [executor, Variant]), replay);
    }

    private static FakeTraceReplay CreateReplay()
        => new FakeTraceReplay().Write(Executor + 0x20, Instruction)
                                .Write(Executor + 0x28, Values)
                                .Write(Executor + 0x80, Context)
                                .Write(Executor + 0x30, Variants)
                                .Write(Values, OutputRowValue)
                                .Write(Values + 0x08, SwitchValue)
                                .Write(Values + 0x10, ScanValue)
                                .Write(OutputRowValue, OutputRowTable)
                                .Write(SwitchValue, SwitchTable)
                                .Write(SwitchValue + 0x08, HashIterator + 0x110)
                                .Write(SwitchValue + 0x10, HashValue)
                                .Write(HashValue, HashTable)
                                .Write(HashValue + 0x2C, Slot)
                                .Write(ScanValue, ScanTable)
                                .Write(ScanValue + 0x08, Rowset)
                                .Write(Context + Slot * sizeof(ulong), Record)
                                .Write(Variant, (byte)0)
                                .Write(Variant + 0x08, 5UL)
                                .Write(Variant + 0x10, 0UL);

    private static TraceHookCall Call(ulong function, ulong id, ulong[] arguments, ulong returnValue = 0)
        => new()
        {
            FunctionAddress = function,
            Id = id,
            CallIndex = (uint)id,
            Position = At(id),
            Thread = Thread,
            Arguments = arguments,
            ReturnValue = returnValue
        };
}
