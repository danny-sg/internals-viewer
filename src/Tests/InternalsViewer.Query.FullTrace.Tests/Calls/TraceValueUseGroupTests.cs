using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Calls;

[Trait("Category", "Unit")]
public class TraceValueUseGroupTests
{
    private const ulong Member = 0x1000;

    private const ulong Static = 0x2000;

    [Fact]
    public void Uses_Are_Grouped_By_How_The_Value_Was_Held()
    {
        TraceValueUse[] uses =
        [
            Use(Static, "RDX"),
            Use(Static, "RAX"),
            Use(Member, "RCX")
        ];

        var groups = TraceValueUseGroup.Build(uses, GetFunctionType);

        Assert.Equal([TraceValueRole.CalledOn, TraceValueRole.Returned, TraceValueRole.Passed], groups.Select(g => g.Role));
    }

    [Fact]
    public void Only_A_Member_Function_Is_Called_On_The_Value_In_RCX()
    {
        var groups = TraceValueUseGroup.Build([Use(Static, "RCX"), Use(0x3000, "RCX")], GetFunctionType);

        var passed = Assert.Single(groups);

        Assert.Equal(TraceValueRole.Passed, passed.Role);
        Assert.Equal(2, passed.Uses.Count);
    }

    [Fact]
    public void A_Group_Counts_The_Calls_Of_Its_Uses()
    {
        TraceValueUse[] uses = [Use(Static, "RDX", calls: 3), Use(0x3000, "R8", calls: 1_200)];

        var group = Assert.Single(TraceValueUseGroup.Build(uses, GetFunctionType));

        Assert.Equal(1_203, group.Calls);
    }

    private static FunctionType? GetFunctionType(TraceValueUse use) => use.FunctionAddress switch
    {
        Member => FunctionType.Member,
        Static => FunctionType.Static,
        _ => null
    };

    private static TraceValueUse Use(ulong address, string location, int calls = 1)
        => new(address, 0, location, calls, FirstCall(), FirstCall());

    private static TraceValueCall FirstCall() => new(0, At(1), null);
}
