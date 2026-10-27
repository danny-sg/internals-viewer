using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Indexes;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class IndexCommandToolsTests
{
    [Fact]
    public async Task Index_Zoom_Passes_The_Page_And_Zoom()
    {
        var commands = new RecordingIndexCommands();

        var tools = new IndexCommandTools(new McpDocumentResolver(new TestAreaTab(McpArea.Index, commands: commands)));

        await tools.Zoom("1:336", 2.5f);

        Assert.Equal((new PageAddress(1, 336), 2.5f), commands.Zoomed);
    }

    [Fact]
    public async Task Index_Select_Page_Without_A_Page_Clears_The_Selection()
    {
        var commands = new RecordingIndexCommands();

        var tools = new IndexCommandTools(new McpDocumentResolver(new TestAreaTab(McpArea.Index, commands: commands)));

        await tools.SelectPage();

        Assert.True(commands.PageCleared);
    }

    [Fact]
    public async Task Index_Select_Row_Without_A_Slot_Clears_The_Row()
    {
        var commands = new RecordingIndexCommands();

        var tools = new IndexCommandTools(new McpDocumentResolver(new TestAreaTab(McpArea.Index, commands: commands)));

        await tools.SelectRow();

        Assert.True(commands.RowCleared);
    }

    private sealed class RecordingIndexCommands : IIndexCommands
    {
        public (PageAddress, float?)? Zoomed { get; private set; }

        public bool PageCleared { get; private set; }

        public bool RowCleared { get; private set; }

        public Task<string> ZoomToPageAsync(PageAddress address, float? zoom)
        {
            Zoomed = (address, zoom);

            return Task.FromResult("zoomed");
        }

        public Task<string> SelectPageAsync(PageAddress? address)
        {
            PageCleared = address is null;

            return Task.FromResult("page");
        }

        public Task<string> SelectRowAsync(int? slot)
        {
            RowCleared = slot is null;

            return Task.FromResult("row");
        }
    }
}