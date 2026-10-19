using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.RowSources;
using InternalsViewer.Query.FullTrace.RowSources.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class RowSourceHookTests
{
    private const uint Thread = 7;

    private const ulong GetRow = 0x1000;

    private const ulong CreateValue = 0x2000;

    private const ulong BatchGetData = 0x2100;

    private const ulong BatchFactory = 0x2200;

    private const ulong Constructor = 0x2300;

    private const ulong Teardown = 0x2400;

    private const ulong PlanObjectTable = 0x7000;

    private const ulong BatchPlanObjectTable = 0x7100;

    private const ulong PlanObject = 0x1F4_0000_7000;

    private const ulong BatchPlanObject = 0x1F4_0000_7100;

    private const ulong Query = 0x1F4_0000_7200;

    private const ulong Parent = 0x1F4_0000_1000;

    private const ulong Child = 0x1F4_0000_2000;

    private const ulong Context = 0x1F4_0000_3000;

    private const ulong ParentRow = 0x1F4_0000_4000;

    private const ulong ChildRow = 0x1F4_0000_5000;

    private const ulong Value = 0x1F4_0000_6000;

    [Fact]
    public void A_Slot_Set_Inside_An_Iterator_Call_Belongs_To_That_Iterator()
    {
        var replay = Slots(0, 0);

        var hook = CreateHook();

        hook.OnEntry(Call(GetRow, 1, Parent), replay);

        replay.Write(Context + 1 * sizeof(ulong), ParentRow);

        hook.OnReturn(Call(GetRow, 1, Parent), replay);

        Assert.Equal((Parent, 0UL), (hook.GetOwner(Context, 1), hook.GetOwner(Context, 0)));
    }

    [Fact]
    public void A_Slot_Set_Inside_A_Nested_Call_Belongs_To_The_Innermost_Iterator()
    {
        var replay = Slots(0, 0);

        var hook = CreateHook();

        hook.OnEntry(Call(GetRow, 1, Parent), replay);

        hook.OnEntry(Call(GetRow, 2, Child), replay);

        replay.Write(Context, ChildRow);

        hook.OnReturn(Call(GetRow, 2, Child), replay);

        replay.Write(Context + 1 * sizeof(ulong), ParentRow);

        hook.OnReturn(Call(GetRow, 1, Parent), replay);

        Assert.Equal((Child, Parent), (hook.GetOwner(Context, 0), hook.GetOwner(Context, 1)));
    }

    [Fact]
    public void A_Slot_Missing_From_The_Trace_Before_The_Call_Has_No_Owner()
    {
        var replay = new FakeTraceReplay();

        var hook = CreateHook();

        hook.OnEntry(Call(GetRow, 1, Parent), replay);

        replay.Write(Context, ParentRow);

        hook.OnReturn(Call(GetRow, 1, Parent), replay);

        Assert.Equal(0UL, hook.GetOwner(Context, 0));
    }

    [Fact]
    public void A_Slot_Whose_Row_Is_Rewritten_Inside_An_Iterator_Call_Belongs_To_That_Iterator()
    {
        var replay = Slots(ParentRow, 0).Write(ParentRow, new byte[64]);

        var hook = CreateHook();

        hook.OnEntry(Call(GetRow, 1, Parent), replay);

        replay.Write(ParentRow + 0x08, 42UL);

        hook.OnReturn(Call(GetRow, 1, Parent), replay);

        Assert.Equal(Parent, hook.GetOwner(Context, 0));
    }

    [Fact]
    public void A_Slot_That_Holds_The_Same_Handle_Before_And_After_The_Call_Has_No_Owner()
    {
        var replay = Slots(ParentRow, 0);

        var hook = CreateHook();

        hook.OnEntry(Call(GetRow, 1, Parent), replay);

        hook.OnReturn(Call(GetRow, 1, Parent), replay);

        Assert.Equal(0UL, hook.GetOwner(Context, 0));
    }

    [Fact]
    public void A_Row_Iterator_Reports_The_Plan_Node_Of_Its_Plan_Object()
    {
        var replay = PlanObjects().Write(Parent + 0x10, PlanObject);

        var hook = CreateHook();

        hook.OnEntry(Call(GetRow, 1, Parent), replay);

        Assert.Equal(new IteratorPlanNode(Parent, PlanObject, 0, Query, 2), Assert.Single(hook.GetPlanNodes()));
    }

    [Fact]
    public void A_Batch_Iterator_Reports_The_Plan_Node_Of_The_Row_Plan_Object_It_Points_To()
    {
        var replay = PlanObjects().Write(Child + 0x10, BatchPlanObject);

        var hook = CreateHook();

        hook.OnEntry(Call(BatchGetData, 1, Child), replay);

        Assert.Equal(new IteratorPlanNode(Child, PlanObject, BatchPlanObject, Query, 2), Assert.Single(hook.GetPlanNodes()));
    }

    [Fact]
    public void A_Batch_Iterator_Built_By_Its_Factory_Reports_Its_Plan_Node()
    {
        var replay = PlanObjects();

        var hook = CreateHook();

        hook.OnEntry(Call(BatchFactory, 1, BatchPlanObject), replay);

        hook.OnReturn(Call(BatchFactory, 1, BatchPlanObject, returnValue: Child, at: 3), replay);

        Assert.Equal(new IteratorPlanNode(Child, PlanObject, BatchPlanObject, Query, 2, new TracePoint(At(1), 1)),
                     Assert.Single(hook.GetPlanNodes()));
    }

    [Fact]
    public void A_Plan_Object_Missing_From_The_Trace_Is_Read_Again_On_A_Later_Call()
    {
        var hook = CreateHook();

        var built = Slots(0, 0).Write(BatchPlanObject, BatchPlanObjectTable);

        hook.OnEntry(Call(BatchFactory, 1, BatchPlanObject), built);

        hook.OnReturn(Call(BatchFactory, 1, BatchPlanObject, returnValue: Child), built);

        Assert.Empty(hook.GetPlanNodes());

        hook.OnEntry(Call(BatchGetData, 2, Parent), PlanObjects());

        Assert.Equal(new IteratorPlanNode(Child, PlanObject, BatchPlanObject, Query, 2, new TracePoint(At(1), 1)),
                     Assert.Single(hook.GetPlanNodes()));
    }

    [Fact]
    public void A_Row_Iterator_Reports_When_Its_Constructor_Ran()
    {
        var replay = PlanObjects().Write(Parent + 0x10, PlanObject);

        var hook = CreateHook();

        hook.OnEntry(Call(Constructor, 1, Parent), replay);

        hook.OnReturn(Call(Constructor, 1, Parent), replay);

        hook.OnEntry(Call(GetRow, 2, Parent), replay);

        Assert.Equal(new TracePoint(At(1), 1), Assert.Single(hook.GetPlanNodes()).Constructed);
    }

    [Fact]
    public void A_Query_Teardown_Ends_When_Its_Call_Returns()
    {
        var hook = CreateHook();

        hook.OnEntry(Call(Teardown, 1, Parent), new FakeTraceReplay());

        hook.OnReturn(Call(Teardown, 1, Parent, at: 9), new FakeTraceReplay());

        Assert.Equal(new QueryTeardown(Thread, new TracePoint(At(9), 9)), Assert.Single(hook.GetTeardowns()));
    }

    [Fact]
    public void An_Iterator_Whose_Plan_Object_Is_Not_A_Plan_Object_Reports_No_Plan_Node()
    {
        var replay = PlanObjects().Write(Parent + 0x10, Value).Write(Value, 0x7200UL);

        var hook = CreateHook();

        hook.OnEntry(Call(GetRow, 1, Parent), replay);

        Assert.Empty(hook.GetPlanNodes());
    }

    [Fact]
    public void A_Value_Belongs_To_The_Iterator_Whose_Create_Call_Returned_It()
    {
        var hook = CreateHook();

        hook.OnEntry(Call(CreateValue, 1, Parent), new FakeTraceReplay());

        hook.OnReturn(Call(CreateValue, 1, Parent, Value), new FakeTraceReplay());

        Assert.Equal(Parent, hook.GetCreator(Value));
    }

    private static FakeTraceReplay Slots(ulong first, ulong second)
        => new FakeTraceReplay().Write(Context, first).Write(Context + sizeof(ulong), second);

    private static FakeTraceReplay PlanObjects()
        => Slots(0, 0).Write(PlanObject, PlanObjectTable)
                      .Write(PlanObject + 0x0C, 2)
                      .Write(PlanObject + 0x28, Query)
                      .Write(BatchPlanObject, BatchPlanObjectTable)
                      .Write(BatchPlanObject + 0x10, PlanObject);

    private static RowSourceHook CreateHook()
        => new(new RowSourceFunctions(new Dictionary<ulong, RowSourceFunctionType>
                                      {
                                          [GetRow] = RowSourceFunctionType.Iterator,
                                          [BatchGetData] = RowSourceFunctionType.BatchIterator,
                                          [BatchFactory] = RowSourceFunctionType.BatchFactory,
                                          [CreateValue] = RowSourceFunctionType.Creator,
                                          [Constructor] = RowSourceFunctionType.Constructor,
                                          [Teardown] = RowSourceFunctionType.Teardown
                                      },
                                      new Dictionary<ulong, PlanObjectClass>
                                      {
                                          [PlanObjectTable] = PlanObjectClass.Row,
                                          [BatchPlanObjectTable] = PlanObjectClass.Batch
                                      }));

    private static TraceHookCall Call(ulong function, ulong id, ulong iterator, ulong returnValue = 0, ulong? at = null)
        => new()
        {
            Function = function,
            Instance = iterator,
            Id = id,
            Call = (uint)id,
            Position = At(at ?? id),
            Instructions = at ?? id,
            Thread = Thread,
            Arguments = new[] { iterator, Context, 0UL, 0UL },
            ReturnValue = returnValue
        };
}
