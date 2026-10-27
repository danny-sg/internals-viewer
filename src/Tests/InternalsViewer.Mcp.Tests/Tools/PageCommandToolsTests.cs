using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Pages;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Pages;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class PageCommandToolsTests
{
    [Fact]
    public async Task Page_Goto_Passes_The_Address_And_Slot_To_The_Tab()
    {
        var commands = new RecordingPageCommands();

        var tab = new TestAreaTab(McpArea.Page, commands: commands);

        var text = await new PageCommandTools(new McpDocumentResolver(tab)).GoTo("1:336", 3);

        Assert.Equal("went", text);
        Assert.Equal((new PageAddress(1, 336), 3), commands.Went);
    }

    [Fact]
    public async Task Page_Select_Marker_Without_A_Name_Clears_The_Selection()
    {
        var commands = new RecordingPageCommands();

        var tab = new TestAreaTab(McpArea.Page, commands: commands);

        await new PageCommandTools(new McpDocumentResolver(tab)).SelectMarker();

        Assert.True(commands.MarkerCleared);
    }

    [Fact]
    public async Task Page_Commands_Fail_Cleanly_For_A_Tab_Of_Another_Area()
    {
        var tab = new TestAreaTab(McpArea.Index);

        await Assert.ThrowsAsync<McpException>(() => new PageCommandTools(new McpDocumentResolver(tab)).Scroll(10));
    }

    [Fact]
    public async Task Command_Errors_Become_Mcp_Errors()
    {
        var tab = new TestAreaTab(McpArea.Page, commands: new RecordingPageCommands { Fail = true });

        await Assert.ThrowsAsync<McpException>(() => new PageCommandTools(new McpDocumentResolver(tab)).Scroll(99999));
    }

    private sealed class RecordingPageCommands : IPageCommands
    {
        public (PageAddress, int?)? Went { get; private set; }

        public bool MarkerCleared { get; private set; }

        public bool Fail { get; init; }

        public Task<string> GoToPageAsync(PageAddress address, int? slot)
        {
            Went = (address, slot);

            return Task.FromResult("went");
        }

        public Task<string> SelectSlotAsync(int slot) => Task.FromResult("slot");

        public Task<string> SelectMarkerAsync(string? name)
        {
            MarkerCleared = name is null;

            return Task.FromResult("marker");
        }

        public Task<string> ScrollToAsync(int offset)
            => Fail ? throw new InvalidOperationException("outside") : Task.FromResult("scrolled");

        public Task<string> ShowTabAsync(PageViewTab tab) => Task.FromResult("tab");

        public Task<string> OpenIndexAsync() => Task.FromResult("index");
    }
}