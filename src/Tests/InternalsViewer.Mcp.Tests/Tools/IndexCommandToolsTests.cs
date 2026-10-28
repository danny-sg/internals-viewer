using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Indexes;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Indexes;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class IndexCommandToolsTests
{
    private static readonly PageAddress Root = new(1, 336);

    private static IndexContext Context => new() { TabId = "t1", Title = "Test", Nodes = [new IndexNode(Root)], Zoom = 1 };

    [Fact]
    public async Task Index_Zoom_Passes_The_Page_And_Reports_The_Zoom_Before_And_After()
    {
        var (tools, commands) = Create();

        var text = await tools.Zoom("1:336", 2.5f);

        Assert.Equal((Root, 2.5f), commands.Zoomed);
        Assert.Contains("from 1 to 2.5", text);
    }

    [Fact]
    public async Task Index_Zoom_Rejects_A_Page_That_Is_Not_In_The_Index()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.Zoom("1:999", 2));

        Assert.Null(commands.Zoomed);
    }

    [Fact]
    public async Task Index_Zoom_Rejects_A_Zoom_Of_Zero_Or_Less()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.Zoom("1:336", 0));

        Assert.Null(commands.Zoomed);
    }

    [Fact]
    public async Task Index_Select_Page_Without_A_Page_Clears_The_Selection()
    {
        var (tools, commands) = Create();

        var text = await tools.SelectPage();

        Assert.True(commands.PageCleared);
        Assert.Equal("Cleared the page selection.", text);
    }

    [Fact]
    public async Task Index_Select_Page_Rejects_A_Page_That_Is_Not_In_The_Index()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.SelectPage("1:999"));

        Assert.False(commands.PageCleared);
    }

    [Fact]
    public async Task Index_Select_Row_Without_A_Slot_Clears_The_Row()
    {
        var (tools, commands) = Create();

        var text = await tools.SelectRow();

        Assert.True(commands.RowCleared);
        Assert.Equal("Cleared the row selection.", text);
    }

    [Fact]
    public async Task Index_Select_Row_Needs_A_Selected_Page()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.SelectRow(1));

        Assert.False(commands.RowSelected);
    }

    private static (IndexCommandTools Tools, RecordingIndexCommands Commands) Create()
    {
        var tab = new TestAreaTab(McpArea.Index, Context with { TabId = "t1", Title = "Test" });

        var commands = new RecordingIndexCommands(tab);

        tab.Commands = commands;

        return (new IndexCommandTools(new McpDocumentResolver(tab)), commands);
    }

    private sealed class RecordingIndexCommands(TestAreaTab tab) : IIndexCommands
    {
        public (PageAddress, float?)? Zoomed { get; private set; }

        public bool PageCleared { get; private set; }

        public bool RowCleared { get; private set; }

        public bool RowSelected { get; private set; }

        private IndexContext Context => (IndexContext)tab.Context!;

        public Task ZoomToPageAsync(PageAddress address, float? zoom)
        {
            Zoomed = (address, zoom);

            tab.Context = Context with { Zoom = zoom ?? 2 };

            return Task.CompletedTask;
        }

        public Task SelectPageAsync(PageAddress? address)
        {
            PageCleared = address is null;

            return Task.CompletedTask;
        }

        public Task SelectRowAsync(int? slot)
        {
            RowCleared = slot is null;

            RowSelected = slot is not null;

            tab.Context = Context with { SelectedSlot = slot };

            return Task.CompletedTask;
        }
    }
}
