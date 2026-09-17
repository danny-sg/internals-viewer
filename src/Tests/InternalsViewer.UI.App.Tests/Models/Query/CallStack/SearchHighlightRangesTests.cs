using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class SearchHighlightRangesTests
{
    [Fact]
    public void Find_Marks_Every_Occurrence_Ignoring_Case()
    {
        var ranges = SearchHighlightRanges.Find("PFVectorBase<Vector>::SetAt(Vector *)", "vector");

        Assert.Equal([(2, 6), (13, 6), (28, 6)], ranges);
    }

    [Fact]
    public void Find_Highlights_Each_Literal_Run_Of_A_Pattern_And_Merges_Overlaps()
    {
        Assert.Equal([(0, 16)], SearchHighlightRanges.Find("XeSqlPkg::vector::Publish", "*XeSqlPkg::vector*"));
        Assert.Equal([(0, 8), (10, 6)], SearchHighlightRanges.Find("XeSqlPkg::vector::Publish", "XeSqlPkg*vector"));
        Assert.Equal([(0, 6)], SearchHighlightRanges.Find("rowrow", "row?ow"));
    }

    [Fact]
    public void Find_Ignores_A_Module_Prefix_And_An_Exact_Match()
    {
        Assert.Equal([(5, 6)], SearchHighlightRanges.Find("void GetRow()", "sqlmin!GetRow"));
        Assert.Empty(SearchHighlightRanges.Find("GetRow", "getrow"));
        Assert.Empty(SearchHighlightRanges.Find("GetRow", "sqlmin!"));
        Assert.Empty(SearchHighlightRanges.Find("GetRow", null));
    }

    [Fact]
    public void Each_Alternative_Of_An_Or_Search_Is_Highlighted()
    {
        Assert.Equal([(0, 10)], SearchHighlightRanges.Find("CQScanSort::GetRow", "sqlmin!CBpQScanSort*|sqlmin!CQScanSort*"));
        Assert.Equal([(0, 13)], SearchHighlightRanges.Find("CQScanTopSort::Init", "CQScanSort*|CQScanTopSort*"));
        Assert.Empty(SearchHighlightRanges.Find("CQScanSort", "CQScanTopSort|cqscansort"));
        Assert.Equal(["a", "b c"], SearchHighlightRanges.Alternatives(" a | b c |"));
    }

    [Fact]
    public void SymbolPart_Strips_The_Module()
    {
        Assert.Equal("CBpQScanColumnStoreScan::BpGetNextBatch",
                     SearchHighlightRanges.SymbolPart(" sqlmin!CBpQScanColumnStoreScan::BpGetNextBatch "));
        Assert.Equal("GetRow", SearchHighlightRanges.SymbolPart("GetRow"));
    }
}
