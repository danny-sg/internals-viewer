using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class ValueUseGroupTests
{
    private const ulong Member = 0x1000;

    private const ulong Static = 0x2000;

    [Fact]
    public void Uses_Are_Grouped_By_How_The_Value_Was_Held()
    {
        TimeTravelValueUse[] uses =
        [
            Use(Static, "*RDX On Entry"),
            Use(Static, "RDX"),
            Use(Static, "RAX"),
            Use(Member, "RCX")
        ];

        var groups = ValueUseGroup.Build(uses, KindOf);

        Assert.Equal(["Calls On It", "Returned It", "Passed It", "Held It Behind A Pointer"], groups.Select(g => g.Title));
    }

    [Fact]
    public void Only_A_Member_Function_Is_Called_On_The_Value_In_RCX()
    {
        var groups = ValueUseGroup.Build([Use(Static, "RCX"), Use(0x3000, "RCX")], KindOf);

        var passed = Assert.Single(groups);

        Assert.Equal(TimeTravelValueRole.Passed, passed.Role);
        Assert.Equal(2, passed.Rows.Count);
    }

    private static FunctionKind? KindOf(TimeTravelValueUse use) => use.Address switch
    {
        Member => FunctionKind.Member,
        Static => FunctionKind.Static,
        _ => null
    };

    private static TimeTravelValueUse Use(ulong address, string location)
        => new(address, 0, location, 1, new TimeTravelValueMatch(0, 1, null), new TimeTravelValueMatch(0, 1, null));
}
