using InternalsViewer.Query.FullTrace.Hooks;
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
        Assert.Equal([(Context, 1)], hook.GetSlots(Parent));
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
    public void A_Value_Belongs_To_The_Iterator_Whose_Create_Call_Returned_It()
    {
        var hook = CreateHook();

        hook.OnEntry(Call(CreateValue, 1, Parent), new FakeTraceReplay());

        hook.OnReturn(Call(CreateValue, 1, Parent, Value), new FakeTraceReplay());

        Assert.Equal(Parent, hook.GetCreator(Value));
    }

    private static FakeTraceReplay Slots(ulong first, ulong second)
        => new FakeTraceReplay().Write(Context, first).Write(Context + sizeof(ulong), second);

    private static RowSourceHook CreateHook()
        => new(new RowSourceFunctions(new Dictionary<ulong, RowSourceFunctionType>
                                      {
                                          [GetRow] = RowSourceFunctionType.Iterator,
                                          [CreateValue] = RowSourceFunctionType.Creator
                                      }));

    private static TraceHookCall Call(ulong function, ulong id, ulong iterator, ulong returnValue = 0)
        => new()
        {
            Function = function,
            Instance = iterator,
            Id = id,
            Call = (uint)id,
            Position = At(id),
            Thread = Thread,
            Arguments = new[] { iterator, Context, 0UL, 0UL },
            ReturnValue = returnValue
        };
}
