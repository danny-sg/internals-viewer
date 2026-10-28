using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Tools;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class DocumentToolsTests
{
    [Fact]
    public async Task List_Tabs_Shows_The_Area_Run_And_Full_Trace()
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create());

        var text = await new TabTools(resolver).ListTabs(CancellationToken.None);

        Assert.Contains("q1", text);
        Assert.Contains("Query", text);
        Assert.Contains("Sales", text);
        Assert.Contains("SELECT * FROM dbo.Orders", text);
        Assert.Contains("Full Trace", text);
    }

    [Fact]
    public async Task Get_Selection_Reports_The_Selected_Call_Node_And_Playhead()
    {
        var run = TestRuns.Create();

        var (resolver, document) = TestRuns.CreateResolver(run);

        var getRow = run.Result.CallStackTree!.Root.ChildNodes.Single().ChildNodes.Single();

        document.Recorded.Selection = new McpSelection { CallNode = getRow, Playhead = TestRuns.At(5, 1) };

        var text = await new DocumentTools(resolver).GetSelection();

        Assert.Contains("[2] sqlmin!CQScanRowsetNew::GetRow", text);
        Assert.Contains("called from: sqlmin!CQueryScan::GetRow", text);
        Assert.Contains("Playhead: 5:1", text);
    }

    [Fact]
    public async Task Get_Selection_Reports_Ranges_Pages_And_Fields()
    {
        var run = TestRuns.Create();

        var (resolver, document) = TestRuns.CreateResolver(run);

        document.Recorded.Selection = new McpSelection
        {
            TraceRange = new McpTraceRange("Position", 10, 20, TestRuns.At(0xA), TestRuns.At(0x14)),
            EventRange = new McpTimeRange(110, 510),
            Scope = new McpTimeRange(10, 1010),
            Pages =
            [
                new McpPageSelection(TestRuns.Page336Address, true, 4, "Id", 4, "00000020:00000130:0001 ModifyRow"),
                new McpPageSelection(TestRuns.Page337Address, false, null, null, null, null)
            ]
        };

        var text = await new DocumentTools(resolver).GetSelection();

        Assert.Contains("Trace timeline shows A:0 to 14:0 (Position axis)", text);
        Assert.Contains("Events timeline shows +0.100 ms to +0.500 ms", text);
        Assert.Contains("Scope handles: +0.000 ms to +1.000 ms", text);
        Assert.Contains("Page (1:336) (active), slot 4, field Id at offset 4, log record 00000020:00000130:0001 ModifyRow", text);
        Assert.Contains("Page (1:337)", text);
        Assert.DoesNotContain("Nothing is selected", text);
    }

    [Fact]
    public async Task An_Unknown_Document_Lists_The_Open_Ones()
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create());

        var exception = await Assert.ThrowsAsync<McpException>(() => new DocumentTools(resolver).GetSelection("q9"));

        Assert.Contains("q1", exception.Message);
    }
}
