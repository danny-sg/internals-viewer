using System.Text.RegularExpressions;
using InternalsViewer.Mcp.Tools.Queries;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools.Queries;

[Trait("Category", "Unit")]
public class CallTreeToolsTests
{
    private const string GetRow = "sqlmin!CQScanRowsetNew::GetRow";

    private const string Caller = "sqlmin!CQueryScan::GetRow";

    [Fact]
    public async Task Call_Tree_Prints_The_Tree_With_Ids_Counts_And_Indentation()
    {
        var text = await Tools().CallTree();

        var lines = Lines(text);

        Assert.Equal("[0] (root)", lines[0]);
        Assert.Equal($"  [1] {Caller}  calls=1", lines[1]);
        Assert.Equal($"    [2] {GetRow}  calls=2  alloc=100 B/1  pages=1  rows=1  events=1", lines[2]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task Call_Tree_Shows_Inclusive_Calls_And_Marks_Subtrees_Below_The_Depth()
    {
        var text = await Tools().CallTree(depth: 1, inclusive: true);

        var lines = Lines(text);

        Assert.Equal("[0] (root)  inclusive calls=3", lines[0]);
        Assert.Equal($"  [1] {Caller}  calls=1  (+1 children below depth 1)  inclusive calls=3", lines[1]);
        Assert.Equal("(1 subtrees continue below depth 1; use root <id> with depth N to open one)", lines[2]);
    }

    [Fact]
    public async Task Call_Tree_Hides_Subtrees_With_Fewer_Calls_Than_Min_Calls()
    {
        var text = await Tools().CallTree(root: 1, minCalls: 3);

        Assert.Equal([$"[1] {Caller}  calls=1"], Lines(text));
    }

    [Fact]
    public async Task Call_Tree_Stops_At_The_Line_Limit()
    {
        var text = await Tools().CallTree(limit: 2);

        var lines = Lines(text);

        Assert.Equal(3, lines.Length);
        Assert.Equal("(stopped after 2 lines; raise limit, lower depth or use minCalls)", lines[2]);
    }

    [Fact]
    public async Task Call_Tree_Scoped_To_An_Operator_Starts_At_Its_Entry_Frames()
    {
        var text = await Tools().CallTree(@operator: 0);

        var lines = Lines(text);

        Assert.Equal("Node 0 Clustered Index Scan: 1 entry frames, cut at 0 exit frames", lines[0]);
        Assert.StartsWith($"[2] {GetRow}  calls=2", lines[1]);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task Call_Tree_Reports_An_Operator_Without_Entry_Frames()
    {
        var text = await Tools().CallTree(@operator: 7);

        Assert.Equal(["no operator event for plan node 7, or it has no entry frames"], Lines(text));
    }

    [Fact]
    public async Task Call_Tree_Find_Lists_Matches_With_Their_Call_Path()
    {
        var text = await Tools().CallTree(find: "GetRow$");

        var lines = Lines(text);

        Assert.Equal("2 matching nodes, most calls first", lines[0]);
        Assert.StartsWith($"[2] {GetRow}  calls=2", lines[1]);
        Assert.Equal($"    called from: {Caller}", lines[2]);
        Assert.Equal($"[1] {Caller}  calls=1", lines[3]);
        Assert.Equal("    called from: ", lines[4]);
    }

    [Fact]
    public async Task Call_Tree_Find_Scoped_To_A_Root_Says_So()
    {
        var text = await Tools().CallTree(find: "GetRow", root: 2, limit: 1);

        Assert.Equal("1 matching nodes in the selected subtree, most calls first", Lines(text)[0]);
    }

    [Fact]
    public async Task Call_Tree_Path_Prints_The_Ancestors_From_The_Root_With_Their_Pages()
    {
        var text = await Tools().CallTree(path: 2);

        var lines = Lines(text);

        Assert.Equal($"[1] {Caller}  calls=1", lines[0]);
        Assert.Equal($"  [2] {GetRow}  calls=2  alloc=100 B/1  pages=1  rows=1  events=1  pages: 1:336", lines[1]);
    }

    [Fact]
    public async Task Call_Tree_Hot_Ranks_Nodes_By_Allocation()
    {
        var text = await Tools().CallTree(hot: 1, by: "alloc");

        Assert.Equal([$"[2] {GetRow}  calls=2  alloc=100 B/1  pages=1  rows=1  events=1  inclusive alloc=100 B/1"], Lines(text));
    }

    [Fact]
    public async Task Call_Tree_Hot_Rejects_An_Unknown_Ranking()
    {
        await Assert.ThrowsAsync<McpException>(() => Tools().CallTree(hot: 5, by: "time"));
    }

    [Fact]
    public async Task Call_Tree_Rejects_An_Unknown_Root()
    {
        var exception = await Assert.ThrowsAsync<McpException>(() => Tools().CallTree(root: 99));

        Assert.Contains("99", exception.Message);
    }

    [Fact]
    public async Task Call_Tree_Falls_Back_To_The_Events_Tree_Without_A_Full_Trace()
    {
        var text = await Tools(withTrace: false).CallTree();

        Assert.StartsWith($"    [2] {GetRow}", Lines(text)[2]);
    }

    [Fact]
    public async Task Functions_Totals_Nodes_Calls_Allocations_And_Pages_Per_Function()
    {
        var text = await Tools().Functions();

        var row = Cells(Lines(text).Single(l => l.StartsWith(GetRow)));

        Assert.Equal([GetRow, "1", "2", "100 B", "1", "1"], row.Take(6));
        Assert.Equal(8, row.Length);
        Assert.Equal("Function", Cells(Lines(text)[0])[0]);
    }

    [Fact]
    public async Task Functions_Filters_By_A_Name_Regex_And_Ranks_By_Allocation()
    {
        var text = await Tools().Functions(grep: "CQueryScan", by: "alloc");

        var lines = Lines(text);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith(Caller, lines[2]);
    }

    [Fact]
    public async Task Functions_Filtered_By_An_Unknown_Category_Lists_Nothing()
    {
        var text = await Tools().Functions(category: "No Such Category");

        Assert.Equal(2, Lines(text).Length);
    }

    [Fact]
    public async Task Functions_Rejects_An_Unknown_Ranking()
    {
        await Assert.ThrowsAsync<McpException>(() => Tools().Functions(by: "time"));
    }

    [Fact]
    public async Task Categories_Totals_Per_Category_With_The_Most_Called_Functions()
    {
        var text = await Tools().Categories(examples: 1);

        var lines = Lines(text);

        Assert.Equal("Category", Cells(lines[0])[0]);
        Assert.Contains(lines, l => l.Contains("CQScanRowsetNew::GetRow x2"));
        Assert.Contains("Categories follow the app's symbol mapping rules.", text);
    }

    [Fact]
    public async Task Locators_Summarises_The_Pages_Seen_As_Call_Arguments()
    {
        var text = await Tools().Locators();

        var lines = Lines(text);

        Assert.Equal("1 distinct pages appear as call arguments, 1 with row slots", lines[0]);
        Assert.Equal(["1:336", "1", "1", $"{GetRow} x1"], Cells(lines[3]));
    }

    [Fact]
    public async Task Locators_For_A_Page_Lists_The_Nodes_That_Touched_It()
    {
        var text = await Tools().Locators(page: "1:336");

        var lines = Lines(text);

        Assert.Equal("page 1:336: 1 row slots touched [4]", lines[0]);
        Assert.StartsWith($"  [2] {GetRow}  calls=2", lines[1]);
        Assert.Equal($"    called from: {Caller}", lines[2]);
    }

    [Fact]
    public async Task Locators_For_A_Node_Lists_Its_Pages_And_Rows()
    {
        var text = await Tools().Locators(node: 2);

        var lines = Lines(text);

        Assert.StartsWith($"[2] {GetRow}", lines[0]);
        Assert.Equal("  pages (1): 1:336", lines[1]);
        Assert.Equal("  rows (1):", lines[2]);
        Assert.Equal("    1:336 slots [4]", lines[3]);
    }

    [Fact]
    public async Task Locators_For_A_Node_Without_Any_Says_So()
    {
        var text = await Tools().Locators(node: 1);

        Assert.Equal("  no locators on this node", Lines(text)[1]);
    }

    [Fact]
    public async Task Locators_Rejects_An_Unknown_Node()
    {
        await Assert.ThrowsAsync<McpException>(() => Tools().Locators(node: 99));
    }

    [Fact]
    public async Task Frames_Lists_The_Distinct_Frames_With_Addresses()
    {
        var text = await Tools().Frames(grep: "GetRow");

        var lines = Lines(text);

        Assert.Equal($"{Caller}  address 0x7FF000000010 rva 0x10", lines[0]);
        Assert.Equal($"{GetRow}  address 0x7FF000000030 rva 0x30 instance 0x9000", lines[1]);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task Frames_Stops_At_The_Limit()
    {
        var text = await Tools().Frames(limit: 1);

        Assert.Equal("(more; use grep or raise limit)", Lines(text)[1]);
    }

    private static CallTreeTools Tools(bool withTrace = true)
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create(withTrace));

        return new CallTreeTools(resolver);
    }

    private static string[] Lines(string text)
        => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private static string[] Cells(string line) => Regex.Split(line.Trim(), @"\s{2,}");
}
