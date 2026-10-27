using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Queries;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class QueryOptionToolsTests
{
    private static QueryContext Context => new()
    {
        TabId = "q1",
        Title = "Test",
        Options =
        [
            new McpOption("full-trace", "Record Full Trace", "Trace", false, true),
            new McpOption("timeline", "Timeline", "View", true, false)
        ]
    };

    [Fact]
    public async Task Options_Lists_Each_Group_With_Its_State()
    {
        var tools = new QueryOptionTools(new McpDocumentResolver(new TestAreaTab(McpArea.Query, Context)));

        var text = await tools.Options();

        Assert.Contains("Trace", text);
        Assert.Contains("full-trace", text);
        Assert.Contains("Timeline", text);
    }

    [Fact]
    public async Task Set_Option_Sends_The_Key_And_State_To_The_Tab()
    {
        var commands = new RecordingNavigator();

        var tools = new QueryOptionTools(new McpDocumentResolver(new TestAreaTab(McpArea.Query, Context, commands)));

        await tools.SetOption("FULL-TRACE", true);

        Assert.Equal(["SetOption full-trace True"], commands.Calls);
    }

    [Fact]
    public async Task Set_Option_Rejects_An_Unknown_Key()
    {
        var tools = new QueryOptionTools(new McpDocumentResolver(new TestAreaTab(McpArea.Query, Context, new RecordingNavigator())));

        var exception = await Assert.ThrowsAsync<McpException>(() => tools.SetOption("nope", true));

        Assert.Contains("full-trace", exception.Message);
    }
}
