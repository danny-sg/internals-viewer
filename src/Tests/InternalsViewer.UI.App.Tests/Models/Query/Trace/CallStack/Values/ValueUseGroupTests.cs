using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Values;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace.CallStack.Values;

public class ValueUseGroupTests
{
    private const ulong Member = 0x1000;

    private const ulong Static = 0x2000;

    [Fact]
    public void Each_Group_Is_Titled_By_How_The_Value_Was_Held()
    {
        TraceValueUse[] uses =
        [
            Use(Static, "RDX"),
            Use(Static, "RAX"),
            Use(Member, "RCX")
        ];

        var groups = TraceValueUseGroup.Build(uses, GetFunctionType).Select(g => ValueUseGroup.Create(g));

        Assert.Equal(["Called by", "Returned by", "Referenced by"], groups.Select(g => g.Title));
    }

    [Fact]
    public void A_Group_Counts_Its_Functions_And_Their_Calls()
    {
        TraceValueUse[] uses = [Use(Static, "RDX", calls: 3), Use(0x3000, "R8", calls: 1_200)];

        var found = Assert.Single(TraceValueUseGroup.Build(uses, GetFunctionType));

        Assert.Equal("2 Functions, 1,203 Calls", ValueUseGroup.Create(found).CountLabel);
    }

    [Fact]
    public void More_Calls_Are_Offered_From_Where_The_Last_Page_Ended()
    {
        var positions = Enumerable.Range(0, 1_250).Select(i => new TracePosition((ulong)i, 0));

        TraceValueCall[] valueCalls = [.. positions.Select((p, i) => new TraceValueCall(i, p, null))];

        var more = ValueNoteRow.More(valueCalls, 500);

        Assert.Equal("Show More (750 Remaining)", more.Label);
        Assert.Equal(500, more.From);
    }

    private static FunctionType? GetFunctionType(TraceValueUse use) => use.FunctionAddress switch
    {
        Member => FunctionType.Member,
        Static => FunctionType.Static,
        _ => null
    };

    private static TraceValueUse Use(ulong address, string location, int calls = 1)
        => new(address, 0, location, calls, FirstCall(), FirstCall());

    private static TraceValueCall FirstCall() => new(0, new TracePosition(1, 0), null);
}
