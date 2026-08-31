using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Query.Plans.Joins;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Joins;

[Trait("Category", "Unit")]
[Trait("Area", "Plans")]
public class JoinTypeParserTests
{
    [Theory]
    [InlineData("Inner Join", JoinType.Inner)]
    [InlineData("Left Outer Join", JoinType.LeftOuter)]
    [InlineData("Right Outer Join", JoinType.RightOuter)]
    [InlineData("Full Outer Join", JoinType.FullOuter)]
    [InlineData("Left Semi Join", JoinType.LeftSemi)]
    [InlineData("Left Anti Semi Join", JoinType.LeftAntiSemi)]
    [InlineData("Right Semi Join", JoinType.RightSemi)]
    [InlineData("Right Anti Semi Join", JoinType.RightAntiSemi)]
    public void Logical_Operator_Is_Mapped(string logicalOperator, JoinType expected)
    {
        Assert.Equal(expected, JoinTypeParser.Parse(logicalOperator));
    }

    [Fact]
    public void Unknown_Or_Missing_Operator_Is_An_Inner_Join()
    {
        Assert.Equal(JoinType.Inner, JoinTypeParser.Parse(null));
        Assert.Equal(JoinType.Inner, JoinTypeParser.Parse(string.Empty));
        Assert.Equal(JoinType.Inner, JoinTypeParser.Parse("Flow Distinct"));
    }
}
