using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceValueUseTests
{
    [Theory]
    [InlineData("RCX", FunctionType.Member, TraceValueRole.CalledOn)]
    [InlineData("RCX", FunctionType.Static, TraceValueRole.Passed)]
    [InlineData("RCX", null, TraceValueRole.Passed)]
    [InlineData("RDX", FunctionType.Member, TraceValueRole.Passed)]
    [InlineData("R9", FunctionType.Free, TraceValueRole.Passed)]
    [InlineData("RAX", FunctionType.Member, TraceValueRole.Returned)]
    public void A_Use_Takes_Its_Role_From_Where_The_Value_Was_Held(string location, FunctionType? type, TraceValueRole role)
    {
        var match = new TraceValueMatch(0, 1, null);

        Assert.Equal(role, new TraceValueUse(0x1000, 0, location, 1, match, match).GetRole(type));
    }
}
