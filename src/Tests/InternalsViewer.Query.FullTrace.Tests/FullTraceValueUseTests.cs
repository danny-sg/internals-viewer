using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class FullTraceValueUseTests
{
    [Theory]
    [InlineData("RCX", FunctionKind.Member, FullTraceValueRole.CalledOn)]
    [InlineData("RCX", FunctionKind.Static, FullTraceValueRole.Passed)]
    [InlineData("RCX", null, FullTraceValueRole.Passed)]
    [InlineData("RDX", FunctionKind.Member, FullTraceValueRole.Passed)]
    [InlineData("R9", FunctionKind.Free, FullTraceValueRole.Passed)]
    [InlineData("RAX", FunctionKind.Member, FullTraceValueRole.Returned)]
    public void A_Use_Takes_Its_Role_From_Where_The_Value_Was_Held(string location, FunctionKind? kind, FullTraceValueRole role)
    {
        var match = new FullTraceValueMatch(0, 1, null);

        Assert.Equal(role, new FullTraceValueUse(0x1000, 0, location, 1, match, match).RoleFor(kind));
    }
}
