using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Tests.UnitTests.Helpers;

[Trait("Category", "Unit")]
public class FuzzyMatchTests
{
    [Theory]
    [InlineData("CQScanHash::GetRow", "hash")]
    [InlineData("CQScanHash::GetRow", "QSHGR")]
    [InlineData("CSLOB_VLDStorePageHeader", "vldheader")]
    public void A_Word_Matches_When_It_Is_Contained_Or_Its_Letters_Are_In_Order(string text, string word)
        => Assert.True(FuzzyMatch.IsMatch(text, word));

    [Theory]
    [InlineData("CQScanHash::GetRow", "rowget")]
    [InlineData("CXVariant", "variants")]
    public void A_Word_Does_Not_Match_When_Its_Letters_Are_Out_Of_Order_Or_Missing(string text, string word)
        => Assert.False(FuzzyMatch.IsMatch(text, word));
}
