using InternalsViewer.Mcp.Documents;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Symbols;

namespace InternalsViewer.Mcp.Tests.Documents;

public class FunctionSearchTests
{
    [Fact]
    public void Search_RanksPrefixAboveContainsAndSkipsNonMatches()
    {
        var nodes = Nodes("IndexRowScanner::GetRow", "CQScanRowsetNew::GetRow", "BTreeRow::SearchPage", "XeSqlPkg::Publish");

        var matches = FunctionSearch.Search(nodes, "getrow", 10);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.Contains("GetRow", m.Name));
    }

    [Fact]
    public void Search_MatchesLettersInOrderAndRequiresEveryWord()
    {
        var nodes = Nodes("BTreeRow::SearchPage", "BTreeRow::GetRow");

        Assert.Single(FunctionSearch.Search(nodes, "srchpg", 10));
        Assert.Single(FunctionSearch.Search(nodes, "btree search", 10));
        Assert.Empty(FunctionSearch.Search(nodes, "btree missing", 10));
    }

    [Fact]
    public void Search_AppliesTheLimit()
    {
        var nodes = Nodes("A::Row1", "A::Row2", "A::Row3");

        Assert.Equal(2, FunctionSearch.Search(nodes, "row", 2).Count);
    }

    private static List<CallStackNode> Nodes(params string[] symbols)
    {
        var tree = new CallStackTree();

        var index = 0;

        var nodes = new List<CallStackNode>();

        foreach (var symbol in symbols)
        {
            var frame = new CallstackFrame
            {
                Module = "sqlmin",
                Rva = (uint)(0x10 + index),
                Address = 0x7FF0_0000_0000 + (ulong)index,
                Resolved = ResolvedCallstackFrameParser.Parse("sqlmin", symbol + "+0x0")
            };

            nodes.Add(tree.AddCall(tree.Root, frame, 1));

            index++;
        }

        return nodes;
    }
}
