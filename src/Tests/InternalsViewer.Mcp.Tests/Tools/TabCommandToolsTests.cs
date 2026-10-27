using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class TabCommandToolsTests
{
    [Fact]
    public async Task Show_Tab_Activates_The_Tab()
    {
        var tab = new TestAreaTab(McpArea.Page);

        var result = await new TabTools(new McpDocumentResolver(tab)).ShowTab("t1", CancellationToken.None);

        Assert.True(tab.Activated);
        Assert.Contains("Switched to t1", result);
    }

    [Fact]
    public async Task Close_Tab_Closes_The_Tab()
    {
        var tab = new TestAreaTab(McpArea.Index);

        var result = await new TabTools(new McpDocumentResolver(tab)).CloseTab("t1", CancellationToken.None);

        Assert.True(tab.Closed);
        Assert.Contains("Closed t1", result);
    }

    [Fact]
    public async Task Show_Tab_Rejects_An_Unknown_Tab()
    {
        var tools = new TabTools(new McpDocumentResolver(new TestAreaTab(McpArea.Page)));

        await Assert.ThrowsAsync<McpException>(() => tools.ShowTab("x9", CancellationToken.None));
    }
}
