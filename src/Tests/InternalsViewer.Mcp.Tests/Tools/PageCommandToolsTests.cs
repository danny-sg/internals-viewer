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
    public async Task Page_Goto_Passes_The_Address_And_Slot_To_The_Tab_And_Reports_The_Loaded_Page()
    {
        var (tools, commands) = Create();

        var text = await tools.GoTo("1:336", 3);

        Assert.Equal((new PageAddress(1, 336), 3), commands.Went);
        Assert.Contains("Loaded page (1:336)", text);
        Assert.Contains("Selected slot 3", text);
    }

    [Fact]
    public async Task Page_Select_Marker_Without_A_Name_Clears_The_Selection()
    {
        var (tools, commands) = Create(new PageContext { TabId = "t1", Title = "Test", SelectedName = "Header" });

        var text = await tools.SelectMarker();

        Assert.True(commands.MarkerCleared);
        Assert.Equal("Cleared the marker selection.", text);
    }

    [Fact]
    public async Task Page_Select_Marker_Reports_The_Marker_The_Tab_Selected()
    {
        var (tools, commands) = Create(new PageContext { TabId = "t1", Title = "Test", MarkerNames = ["Header", "Slot Array"] });

        var text = await tools.SelectMarker("slot");

        Assert.Equal("slot", commands.Marker);
        Assert.Contains("Selected Slot Array", text);
    }

    [Fact]
    public async Task Page_Select_Marker_Rejects_A_Name_Not_In_The_Context_Without_Running_The_Command()
    {
        var (tools, commands) = Create(new PageContext { TabId = "t1", Title = "Test", MarkerNames = ["Header"] });

        var exception = await Assert.ThrowsAsync<McpException>(() => tools.SelectMarker("nope"));

        Assert.Contains("Header", exception.Message);
        Assert.Null(commands.Marker);
    }

    [Fact]
    public async Task Page_Select_Slot_Needs_A_Loaded_Page()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.SelectSlot(1));

        Assert.Null(commands.Slot);
    }

    [Fact]
    public async Task Page_Scroll_Rejects_An_Offset_Outside_The_Page()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.Scroll(99999));

        Assert.Null(commands.Scrolled);
    }

    [Fact]
    public async Task Page_Scroll_Runs_The_Command()
    {
        var (tools, commands) = Create();

        await tools.Scroll(10);

        Assert.Equal(10, commands.Scrolled);
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
        var (tools, commands) = Create();

        commands.Fail = true;

        await Assert.ThrowsAsync<McpException>(() => tools.Scroll(10));
    }

    private static (PageCommandTools Tools, RecordingPageCommands Commands) Create(PageContext? context = null)
    {
        var tab = new TestAreaTab(
            McpArea.Page,
            (context ?? new PageContext { TabId = "t1", Title = "Test" }) with { TabId = "t1", Title = "Test" });

        var commands = new RecordingPageCommands(tab);

        tab.Commands = commands;

        return (new PageCommandTools(new McpDocumentResolver(tab)), commands);
    }

    private sealed class RecordingPageCommands(TestAreaTab tab) : IPageCommands
    {
        public (PageAddress, int?)? Went { get; private set; }

        public string? Marker { get; private set; }

        public bool MarkerCleared { get; private set; }

        public int? Slot { get; private set; }

        public int? Scrolled { get; private set; }

        public bool Fail { get; set; }

        private PageContext Context => (PageContext)tab.Context!;

        public Task GoToPageAsync(PageAddress address, int? slot)
        {
            Went = (address, slot);

            tab.Context = Context with { Address = address, CurrentSlot = slot };

            return Task.CompletedTask;
        }

        public Task SelectSlotAsync(int slot)
        {
            Slot = slot;

            return Task.CompletedTask;
        }

        public Task SelectMarkerAsync(string? name)
        {
            Marker = name;

            MarkerCleared = name is null;

            tab.Context = Context with
            {
                SelectedName = name is null ? null : Context.MarkerNames.First(n => n.Contains(name, StringComparison.OrdinalIgnoreCase)),
                SelectionStart = 4,
                Selection = name is null ? null : new byte[2]
            };

            return Task.CompletedTask;
        }

        public Task ScrollToAsync(int offset)
        {
            Scrolled = offset;

            return Fail ? throw new InvalidOperationException("outside") : Task.CompletedTask;
        }

        public Task ShowTabAsync(PageViewTab view) => Task.CompletedTask;

        public Task OpenIndexAsync() => Task.CompletedTask;
    }
}
