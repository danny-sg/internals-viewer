using InternalsViewer.Mcp.Tools;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class NavigationToolsTests
{
    [Fact]
    public async Task Show_Call_Opens_The_Node_And_Describes_It()
    {
        var (resolver, document) = TestRuns.CreateResolver(TestRuns.Create());

        var text = await new NavigationTools(resolver).ShowCall(2, 1);

        Assert.Equal("ShowCall CQScanRowsetNew::GetRow 1", Assert.Single(document.Recorded.Calls));
        Assert.Contains("[2] sqlmin!CQScanRowsetNew::GetRow  calls=2  alloc=100 B/1  pages=1  rows=1  events=1", text);
        Assert.Contains("showing call #1", text);
    }

    [Fact]
    public async Task Show_Page_Parses_The_Address_And_Slot()
    {
        var (resolver, document) = TestRuns.CreateResolver(TestRuns.Create());

        var text = await new NavigationTools(resolver).ShowPage("1:336", 4);

        Assert.Equal("ShowPage (1:336) 4", Assert.Single(document.Recorded.Calls));
        Assert.Contains("slot 4", text);
    }

    [Fact]
    public async Task Show_Plan_Node_Uses_The_Operator_Event_Identifier()
    {
        var (resolver, document) = TestRuns.CreateResolver(TestRuns.Create());

        var text = await new NavigationTools(resolver).ShowPlanNode(0);

        Assert.Equal("ShowPlanNode 1:0", Assert.Single(document.Recorded.Calls));
        Assert.Contains("Clustered Index Scan", text);
    }

    [Fact]
    public async Task Set_Playhead_Needs_A_Full_Trace()
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create(withTrace: false));

        await Assert.ThrowsAsync<McpException>(() => new NavigationTools(resolver).SetPlayhead("A:1"));
    }

    [Fact]
    public async Task Set_Playhead_Parses_A_Hex_Position()
    {
        var (resolver, document) = TestRuns.CreateResolver(TestRuns.Create());

        await new NavigationTools(resolver).SetPlayhead("1A:F");

        Assert.Equal("SetPlayhead 1A:F", Assert.Single(document.Recorded.Calls));
    }

    [Fact]
    public async Task Show_Event_Selects_The_Event_By_Id()
    {
        var (resolver, document) = TestRuns.CreateResolver(TestRuns.Create());

        var text = await new NavigationTools(resolver).ShowEvent(1);

        Assert.Equal("ShowEvent Clustered Index Scan", Assert.Single(document.Recorded.Calls));
        Assert.Contains("[1]", text);
    }
}
