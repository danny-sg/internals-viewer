using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.CallStack.Locators.Enums;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceCallConditionTests
{
    private const ulong Object = 0x1F4_0000_1000;

    private static readonly ArgumentSlot This = new("this", "this", ArgumentLocation.Register, 0, "RCX");

    private static readonly ArgumentSlot Count = new("count", "int", ArgumentLocation.Register, 1, "RDX");

    private static readonly ArgumentSlot Flag = new("isForward", "bool", ArgumentLocation.Register, 2, "R8");

    private static readonly ArgumentSlot Result = new("result", "CXVariant *", ArgumentLocation.Register, 2, "R8");

    private static readonly ArgumentSlot Page = new("pageId", "PageId const &", ArgumentLocation.Register, 1, "RDX");

    [Theory]
    [InlineData(TraceConditionOperator.Equal, "-1", true)]
    [InlineData(TraceConditionOperator.LessThan, "0", true)]
    [InlineData(TraceConditionOperator.GreaterThan, "0", false)]
    [InlineData(TraceConditionOperator.NotEqual, "4294967295", true)]
    public void An_Int_Argument_Compares_As_A_Signed_Value(TraceConditionOperator comparison, string value, bool expected)
        => Assert.Equal(expected, Argument(Count, comparison, value).Matches(Call(0xFFFF_FFFF)));

    [Fact]
    public void A_Hex_Value_Compares_With_A_Pointer()
        => Assert.True(Argument(This, TraceConditionOperator.Equal, "0x1F400001000").Matches(Call(0, Object)));

    [Fact]
    public void A_Value_That_Is_Not_A_Number_Compares_With_The_Decoded_Reading()
    {
        var call = Call(0, 0, 0, 1);

        Assert.Equal((true, false),
                     (Argument(Flag, TraceConditionOperator.Equal, "true").Matches(call),
                      Argument(Flag, TraceConditionOperator.Equal, "false").Matches(call)));
    }

    [Fact]
    public void The_Return_Value_Only_Matches_A_Call_That_Returned()
    {
        var condition = new TraceCallCondition(TraceConditionTarget.Return, null, "int", TraceConditionOperator.Equal, "0");

        Assert.Equal((true, false),
                     (condition.Matches(Call(0) with { Returned = true }), condition.Matches(Call(0) with { Returned = false })));
    }

    [Fact]
    public void A_Variant_Compares_The_Value_It_Holds()
    {
        var call = Call(0) with { Locators = [VariantLocator.Create(Result.Index, VariantStatus.Value, 1, 0)] };

        Assert.True(Argument(Result, TraceConditionOperator.Equal, "1").Matches(call));
    }

    [Fact]
    public void A_Null_Variant_Matches_Null_But_No_Number()
    {
        var call = Call(0) with { Locators = [VariantLocator.Create(Result.Index, VariantStatus.Null, 0, 0)] };

        Assert.Equal((true, false),
                     (Argument(Result, TraceConditionOperator.Equal, "NULL").Matches(call),
                      Argument(Result, TraceConditionOperator.Equal, "0").Matches(call)));
    }

    [Fact]
    public void A_Page_Compares_As_Its_Text()
    {
        var page = new PageAddress(1, 1536);

        var call = Call(0) with { Locators = [new Locator(Page.Index, LocatorValueType.Page, (1ul << 32) | 1536)] };

        Assert.True(Argument(Page, TraceConditionOperator.Equal, page.ToString()).Matches(call));
    }

    [Fact]
    public void Every_Condition_Has_To_Match()
    {
        var call = Call(5, Object);

        TraceCallCondition[] conditions =
        [
            Argument(This, TraceConditionOperator.Equal, "0x1F400001000"),
            Argument(Count, TraceConditionOperator.GreaterThan, "4")
        ];

        Assert.Equal((true, false),
                     (TraceCallCondition.MatchesAll(conditions, call),
                      TraceCallCondition.MatchesAll([.. conditions, Argument(Count, TraceConditionOperator.Equal, "6")], call)));
    }

    private static TraceCallCondition Argument(ArgumentSlot slot, TraceConditionOperator comparison, string value)
        => new(TraceConditionTarget.Argument, slot, slot.Type, comparison, value);

    private static TraceArgumentCall Call(ulong count, ulong instance = 0, ulong returnValue = 0, ulong flag = 0)
    {
        var slots = new ulong[ArgumentLayout.CapturedSlots];

        slots[0] = instance;

        slots[1] = count;

        slots[2] = flag;

        return new TraceArgumentCall(default, 7, true, slots, returnValue);
    }
}
