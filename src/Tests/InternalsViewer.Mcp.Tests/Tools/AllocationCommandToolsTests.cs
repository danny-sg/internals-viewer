using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Allocations;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Allocations;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class AllocationCommandToolsTests
{
    [Fact]
    public async Task Allocations_Select_Object_Passes_The_Name_And_Add_And_Reports_The_Selection()
    {
        var (tools, commands) = Create();

        var text = await tools.SelectObject("Orders", true);

        Assert.Equal(("Orders", true), commands.Selected);
        Assert.Equal("Selected: Orders.", text);
    }

    [Fact]
    public async Task Allocations_Select_Object_Without_A_Name_Reports_The_Cleared_Selection()
    {
        var (tools, _) = Create(new AllocationsContext { TabId = "t1", Title = "Test", SelectedObjects = ["Orders"] });

        var text = await tools.SelectObject();

        Assert.Equal("Cleared the object selection.", text);
    }

    [Fact]
    public async Task Allocations_Scroll_Parses_The_Page_And_Reports_Where_The_Tab_Scrolled()
    {
        var (tools, commands) = Create(new AllocationsContext { TabId = "t1", Title = "Test", Files = [new DatabaseFile(1)] });

        var text = await tools.Scroll(page: "1:1048");

        Assert.Equal((null, new PageAddress(1, 1048)), commands.Scrolled);
        Assert.Contains("1:1048", text);
    }

    [Fact]
    public async Task Allocations_Scroll_Needs_A_Name_Or_A_Page()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.Scroll());

        Assert.Null(commands.Scrolled);
    }

    [Fact]
    public async Task Allocations_Scroll_Rejects_A_File_The_Database_Does_Not_Have()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.Scroll(page: "7:1"));

        Assert.Null(commands.Scrolled);
    }

    [Fact]
    public async Task Allocations_Overlay_Reports_The_Overlay_The_Tab_Shows()
    {
        var (tools, commands) = Create();

        var text = await tools.Overlay(AllocationOverlay.Gam);

        Assert.Equal(AllocationOverlay.Gam, commands.Overlay);
        Assert.Equal("Showing the GAM overlay.", text);
    }

    [Fact]
    public async Task Allocations_Overlay_Without_A_Value_Turns_It_Off()
    {
        var (tools, commands) = Create(new AllocationsContext { TabId = "t1", Title = "Test", Overlay = "GAM" });

        var text = await tools.Overlay();

        Assert.Null(commands.Overlay);
        Assert.Equal("Turned the overlay off.", text);
    }

    [Fact]
    public async Task Allocations_Buffer_Pool_Overlay_Needs_A_Server_Connection()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.Overlay(AllocationOverlay.BufferPool));

        Assert.False(commands.OverlaySet);
    }

    [Fact]
    public async Task Allocations_Search_Reports_The_Filter_And_Clears_It()
    {
        var (tools, commands) = Create();

        Assert.Equal("Searching for 'ord'.", await tools.Search("ord"));
        Assert.Equal("Cleared the search text.", await tools.Search());
        Assert.Null(commands.Search);
    }

    private static (AllocationCommandTools Tools, RecordingAllocationsCommands Commands) Create(AllocationsContext? context = null)
    {
        var tab = new TestAreaTab(
            McpArea.Allocations,
            (context ?? new AllocationsContext { TabId = "t1", Title = "Test" }) with { TabId = "t1", Title = "Test" });

        var commands = new RecordingAllocationsCommands(tab);

        tab.Commands = commands;

        return (new AllocationCommandTools(new McpDocumentResolver(tab)), commands);
    }

    private sealed class RecordingAllocationsCommands(TestAreaTab tab) : IAllocationsCommands
    {
        public (string?, bool)? Selected { get; private set; }

        public (string?, PageAddress?)? Scrolled { get; private set; }

        public AllocationOverlay? Overlay { get; private set; }

        public bool OverlaySet { get; private set; }

        public string? Search { get; private set; }

        private AllocationsContext Context => (AllocationsContext)tab.Context!;

        public Task SelectObjectAsync(string? name, bool add)
        {
            Selected = (name, add);

            tab.Context = Context with { SelectedObjects = name is null ? [] : [name] };

            return Task.CompletedTask;
        }

        public Task<PageAddress> ScrollToAsync(string? name, PageAddress? page)
        {
            Scrolled = (name, page);

            return Task.FromResult(page ?? new PageAddress(1, 8));
        }

        public Task SetOverlayAsync(AllocationOverlay? overlay)
        {
            Overlay = overlay;

            OverlaySet = true;

            tab.Context = Context with { Overlay = overlay?.ToString().ToUpperInvariant() };

            return Task.CompletedTask;
        }

        public Task SetSearchAsync(string? text)
        {
            Search = text;

            tab.Context = Context with { Filter = text ?? string.Empty };

            return Task.CompletedTask;
        }
    }
}
