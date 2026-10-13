using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceValueUseTests
{
    [Theory]
    [InlineData("RCX", FunctionKind.Member, TraceValueRole.CalledOn)]
    [InlineData("RCX", FunctionKind.Static, TraceValueRole.Passed)]
    [InlineData("RCX", null, TraceValueRole.Passed)]
    [InlineData("RDX", FunctionKind.Member, TraceValueRole.Passed)]
    [InlineData("R9", FunctionKind.Free, TraceValueRole.Passed)]
    [InlineData("RAX", FunctionKind.Member, TraceValueRole.Returned)]
    public void A_Use_Takes_Its_Role_From_Where_The_Value_Was_Held(string location, FunctionKind? kind, TraceValueRole role)
    {
        var match = new TraceValueMatch(0, 1, null);

        Assert.Equal(role, new TraceValueUse(0x1000, 0, location, 1, match, match).RoleFor(kind));
    }
}
