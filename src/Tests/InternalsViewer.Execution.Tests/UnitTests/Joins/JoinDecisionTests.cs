using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.Services.Joins;

namespace InternalsViewer.Execution.Tests.UnitTests.Joins;

public class JoinDecisionTests
{
    [Theory]
    [InlineData(JoinType.Inner, true, true, true)]
    [InlineData(JoinType.Inner, true, false, false)]
    [InlineData(JoinType.Inner, false, true, false)]
    [InlineData(JoinType.LeftOuter, true, true, true)]
    [InlineData(JoinType.LeftOuter, true, false, true)]
    [InlineData(JoinType.LeftOuter, false, true, false)]
    [InlineData(JoinType.RightOuter, true, false, false)]
    [InlineData(JoinType.RightOuter, false, true, true)]
    [InlineData(JoinType.FullOuter, true, false, true)]
    [InlineData(JoinType.FullOuter, false, true, true)]
    [InlineData(JoinType.LeftSemi, true, true, true)]
    [InlineData(JoinType.LeftSemi, true, false, false)]
    [InlineData(JoinType.LeftAntiSemi, true, false, true)]
    [InlineData(JoinType.LeftAntiSemi, true, true, false)]
    [InlineData(JoinType.RightAntiSemi, false, true, true)]
    [InlineData(JoinType.RightAntiSemi, true, true, false)]
    public void Decision_Emits_When_What_Was_Found_Satisfies_The_Rule(JoinType type, bool hasOuter, bool hasInner, bool isEmitted)
    {
        Assert.Equal(isEmitted, type.Decide(hasOuter, hasInner).IsEmitted);
    }

    /// <summary>
    /// The badge states the rule the engine applies, so the two have to agree on which side survives without a partner
    /// </summary>
    [Theory]
    [InlineData(JoinType.Inner)]
    [InlineData(JoinType.LeftOuter)]
    [InlineData(JoinType.RightOuter)]
    [InlineData(JoinType.FullOuter)]
    [InlineData(JoinType.LeftSemi)]
    [InlineData(JoinType.LeftAntiSemi)]
    [InlineData(JoinType.RightSemi)]
    [InlineData(JoinType.RightAntiSemi)]
    public void Decision_Agrees_With_The_Preserve_Rules_The_Engine_Uses(JoinType type)
    {
        Assert.Equal(type.PreservesOuter(), type.Decide(true, false).IsEmitted);
        Assert.Equal(type.PreservesInner(), type.Decide(false, true).IsEmitted);
    }
}
