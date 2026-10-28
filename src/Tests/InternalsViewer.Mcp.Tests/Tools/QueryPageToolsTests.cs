using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Pages;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Pages;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class QueryPageToolsTests
{
    private static readonly PageAddress Address = new(1, 336);

    [Fact]
    public async Task Query_Page_Read_Reads_The_Page_In_The_Dock_With_Its_Structures()
    {
        var (tools, _, _) = Create();

        var text = await tools.Read();

        Assert.Contains("Page (1:336) Data", text);
        Assert.Contains("structures selectable as slots: Page Header = -1, Offset Table = -2", text);
    }

    [Fact]
    public async Task Query_Page_Select_Slot_Runs_On_The_Page_In_The_Dock()
    {
        var (tools, commands, _) = Create();

        var text = await tools.SelectSlot(-1);

        Assert.Equal(-1, commands.Slot);
        Assert.Contains("(Page Header)", text);
    }

    [Fact]
    public async Task Query_Page_Select_Slot_Rejects_A_Slot_The_Page_Does_Not_Have()
    {
        var (tools, commands, _) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.SelectSlot(9));

        Assert.Null(commands.Slot);
    }

    [Fact]
    public async Task Query_Page_Goto_Opens_The_Page_In_The_Query_Tab()
    {
        var (tools, _, navigator) = Create();

        var text = await tools.GoTo("1:336", 2);

        Assert.Equal(["ShowPage (1:336) 2"], navigator.Calls);
        Assert.Contains("Loaded page (1:336) (Data).", text);
    }

    [Fact]
    public async Task Without_A_Page_In_The_Dock_The_Error_Says_How_To_Open_One()
    {
        var query = new TestAreaTab(McpArea.Query, new QueryContext { TabId = "q1", Title = "Query" }) { Id = "q1" };

        var tools = new QueryPageTools(new McpDocumentResolver(query));

        var exception = await Assert.ThrowsAsync<McpException>(() => tools.SelectMarker("Null Bitmap"));

        Assert.Contains("No page is open", exception.Message);
        Assert.Contains("query_page_goto", exception.Message);
    }

    private static (QueryPageTools Tools, RecordingPageCommands Commands, RecordingNavigator Navigator) Create()
    {
        var page = new DataPage
        {
            PageAddress = Address,
            Data = new byte[PageData.Size],
            PageHeader = new PageHeader { PageType = PageType.Data, SlotCount = 2 }
        };

        var context = new PageContext
        {
            TabId = "q1/1:336",
            Title = "Page 1:336",
            Page = page,
            Address = Address,
            Structures = [new PageStructure(-1, "Page Header"), new PageStructure(-2, "Offset Table")]
        };

        var pageTab = new TestAreaTab(McpArea.Page, context) { Id = "q1/1:336" };

        var commands = new RecordingPageCommands();

        pageTab.Commands = commands;

        var navigator = new RecordingNavigator();

        var query = new TestAreaTab(McpArea.Query, new QueryContext { TabId = "q1", Title = "Query" }, navigator)
        {
            Id = "q1",
            Page = pageTab
        };

        return (new QueryPageTools(new McpDocumentResolver(query)), commands, navigator);
    }

    private sealed class RecordingPageCommands : IPageCommands
    {
        public int? Slot { get; private set; }

        public Task GoToPageAsync(PageAddress address, int? slot) => Task.CompletedTask;

        public Task SelectSlotAsync(int slot)
        {
            Slot = slot;

            return Task.CompletedTask;
        }

        public Task SelectMarkerAsync(string? name) => Task.CompletedTask;

        public Task ScrollToAsync(int offset) => Task.CompletedTask;

        public Task ShowTabAsync(PageViewTab tab) => Task.CompletedTask;

        public Task OpenIndexAsync() => Task.CompletedTask;
    }
}
