using InternalsViewer.Query.CallStack.Categories;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class GlobPatternTests
{
    [Theory]
    [InlineData("CQScan", "CQScan", true)]        // exact
    [InlineData("CQScan", "cqscan", true)]        // exact, case-insensitive
    [InlineData("CQScan", "CQScanHash", false)]   // exact does not prefix-match
    [InlineData("CQScan*", "CQScanHash", true)]   // starts-with
    [InlineData("CQScan*", "CQScan", true)]
    [InlineData("CQScan*", "XCQScan", false)]
    [InlineData("*Scan", "CQScan", true)]         // ends-with
    [InlineData("*Scan", "ScanX", false)]
    [InlineData("*QDS*", "CAutoQdsDBLock", true)] // contains, case-insensitive
    [InlineData("*QDS*", "NoMatch", false)]
    [InlineData("*", "anything", true)]           // wildcard
    [InlineData("*", null, true)]
    [InlineData("CQScan", null, false)]
    public void Matches(string pattern, string? value, bool expected) =>
        Assert.Equal(expected, GlobPattern.Parse(pattern).Matches(value));

    [Fact]
    public void Exact_Scores_Higher_Than_Any_Glob() =>
        Assert.True(GlobPattern.Parse("CQScan").Score > GlobPattern.Parse("CQScanHashAggAndLonger*").Score);

    [Fact]
    public void Longer_Glob_Scores_Higher_Than_Shorter() =>
        Assert.True(GlobPattern.Parse("CQScanHash*").Score > GlobPattern.Parse("CQScan*").Score);

    [Fact]
    public void Wildcard_Scores_Zero() =>
        Assert.Equal(0, GlobPattern.Parse("*").Score);
}
