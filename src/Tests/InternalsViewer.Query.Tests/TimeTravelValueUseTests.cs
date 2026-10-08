using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelValueUseTests
{
    [Theory]
    [InlineData("RCX", FunctionKind.Member, TimeTravelValueRole.CalledOn)]
    [InlineData("RCX", FunctionKind.Static, TimeTravelValueRole.Passed)]
    [InlineData("RCX", null, TimeTravelValueRole.Passed)]
    [InlineData("RDX", FunctionKind.Member, TimeTravelValueRole.Passed)]
    [InlineData("[RSP+0x28]", FunctionKind.Free, TimeTravelValueRole.Passed)]
    [InlineData("RAX", FunctionKind.Member, TimeTravelValueRole.Returned)]
    [InlineData("*RDX On Return", FunctionKind.Member, TimeTravelValueRole.BehindPointer)]
    public void A_Use_Takes_Its_Role_From_Where_The_Value_Was_Held(string location, FunctionKind? kind, TimeTravelValueRole role)
    {
        var match = new TimeTravelValueMatch(0, 1, null);

        Assert.Equal(role, new TimeTravelValueUse(0x1000, 0, location, 1, match, match).RoleFor(kind));
    }
}
