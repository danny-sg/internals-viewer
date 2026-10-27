using System.Text.RegularExpressions;
using InternalsViewer.Mcp.Tools.Queries;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools.Queries;

[Trait("Category", "Unit")]
public class RunToolsTests
{
    [Fact]
    public async Task Summary_Describes_The_Run()
    {
        var text = await Tools().Summary();

        Assert.Contains("Server:        .\\SQL2025", text);
        Assert.Contains("Database:      Sales", text);
        Assert.Contains("Success:       True", text);
        Assert.Contains("Rows returned: 120", text);
        Assert.Contains("Query window:  0.010 ms to 1.010 ms capture-relative (1.000 ms)", text);
        Assert.Contains("  SELECT * FROM dbo.Orders", text);
        Assert.Contains("Query options: ClearBufferPool=False, DisableReadAhead=True", text);
        Assert.Contains("Lock categories captured:", text);
        Assert.Contains("Full Trace:    yes", text);
        Assert.Contains("Call Log Calls", text);
        Assert.Contains("  plan 0 handle 1 internal=False 1 nodes", text);
        Assert.Contains("  3 nodes, 7 events, 0 log records", text);
        Assert.Contains("  ExecutionOperatorEvent            2 / 2", text);
        Assert.Contains("  (120 rows affected)", text);
    }

    [Fact]
    public async Task Summary_Says_When_There_Is_No_Full_Trace()
    {
        var text = await Tools(withTrace: false).Summary();

        Assert.Contains("Full Trace:    no", text);
        Assert.DoesNotContain("Call Log Calls", text);
    }

    [Fact]
    public async Task Plan_Prints_The_Operator_Tree_From_The_Showplan_Xml()
    {
        var text = await Tools().Plan();

        Assert.Contains("Statement: SELECT est rows 100 cost 0.1 hash 0x1 plan hash 0x2", text);
        Assert.Contains("  SELECT * FROM dbo.Orders", text);
        Assert.Contains("QueryPlan: DOP 1 compile 2 ms cached size 16 KB", text);
        Assert.Contains("  memory grant: SerialRequiredMemory=0, SerialDesiredMemory=0", text);
        Assert.Contains("  elapsed 5 ms, cpu 4 ms", text);
        Assert.Contains("  waits: PAGEIOLATCH_SH 1 ms x2", text);
        Assert.Contains("[0] Clustered Index Scan (Clustered Index Scan) dbo.Orders.PK_Orders est 100 actual 120 rows, 1 exec, " +
                        "2 logical reads, Row",
                        text);
        Assert.DoesNotContain("predicate:", text);
    }

    [Fact]
    public async Task Plan_Detail_Adds_Predicates()
    {
        var text = await Tools().Plan(detail: true);

        Assert.Contains("    predicate: [dbo].[Orders].[Id]>(1)", text);
    }

    [Fact]
    public async Task Plan_Xml_Returns_The_Raw_Showplan()
    {
        var text = await Tools().Plan(xml: true);

        Assert.Equal(TestRuns.PlanXml, text);
    }

    [Fact]
    public async Task Plan_Rejects_An_Index_Out_Of_Range()
    {
        await Assert.ThrowsAsync<McpException>(() => Tools().Plan(index: 3));
    }

    [Fact]
    public async Task Operators_Lists_Each_Operator_With_Its_Timing()
    {
        var text = await Tools().Operators();

        var lines = Lines(text);

        Assert.Equal("Node Parent Operator Logical Target Start Dur ms First row Rows Thr Entry Inst", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("-1 SELECT"));
        Assert.Contains("0 -1 Clustered Index Scan Clustered Index Scan Orders.PK_Orders +0.040 0.900 +0.110 120 1 1 1", lines);
    }

    [Fact]
    public async Task Operators_Frames_Lists_Entry_Frames_Instances_And_Lifetimes()
    {
        var text = await Tools().Operators(frames: true);

        Assert.Contains("Node 0 Clustered Index Scan:", text);
        Assert.Contains("  entry [2] sqlmin!CQScanRowsetNew::GetRow", text);
        Assert.Contains("  iterator instance 0x9000", text);
        Assert.Contains("  lifetime thread 7 from", text);
        Assert.Contains("  thread 0: start +0.040 ms, 0.900 ms, 120 rows", text);
    }

    private static RunTools Tools(bool withTrace = true) => new(TestRuns.CreateResolver(TestRuns.Create(withTrace)).Resolver);

    private static List<string> Lines(string text)
        => [.. text.Split('\n').Select(l => Regex.Replace(l.Trim(), " {2,}", " "))];
}
