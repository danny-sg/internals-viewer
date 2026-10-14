using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class ValueUseGroupTests
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

        var groups = ValueUseGroup.Build(uses, KindOf);

        Assert.Equal(["Called by", "Returned by", "Referenced by"], groups.Select(g => g.Title));
    }

    [Fact]
    public void Only_A_Member_Function_Is_Called_On_The_Value_In_RCX()
    {
        var groups = ValueUseGroup.Build([Use(Static, "RCX"), Use(0x3000, "RCX")], KindOf);

        var passed = Assert.Single(groups);

        Assert.Equal(TraceValueRole.Passed, passed.Role);
        Assert.Equal(2, passed.Rows.Count);
    }

    [Fact]
    public void A_Group_Counts_Its_Functions_And_Their_Calls()
    {
        var group = Assert.Single(ValueUseGroup.Build([Use(Static, "RDX", calls: 3), Use(0x3000, "R8", calls: 1_200)], KindOf));

        Assert.Equal("2 Functions, 1,203 Calls", group.CountLabel);
    }

    [Fact]
    public void More_Calls_Are_Offered_From_Where_The_Last_Page_Ended()
    {
        TraceValueMatch[] matches = [.. Enumerable.Range(0, 1_250).Select(i => new TraceValueMatch(i, (ulong)i, null))];

        var more = ValueNoteRow.More(matches, 500);

        Assert.Equal("Show More (750 Remaining)", more.Label);
        Assert.Equal(500, more.From);
    }

    private static FunctionKind? KindOf(TraceValueUse use) => use.Address switch
    {
        Member => FunctionKind.Member,
        Static => FunctionKind.Static,
        _ => null
    };

    private static TraceValueUse Use(ulong address, string location, int calls = 1)
        => new(address, 0, location, calls, new TraceValueMatch(0, 1, null), new TraceValueMatch(0, 1, null));
}
