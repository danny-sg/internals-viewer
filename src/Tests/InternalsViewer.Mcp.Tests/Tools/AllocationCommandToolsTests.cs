using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Allocations;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Allocations;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class AllocationCommandToolsTests
{
    [Fact]
    public async Task Allocations_Select_Object_Passes_The_Name_And_Add()
    {
        var commands = new RecordingAllocationsCommands();

        await CreateTools(commands).SelectObject("Orders", true);

        Assert.Equal(("Orders", true), commands.Selected);
    }

    [Fact]
    public async Task Allocations_Scroll_Parses_The_Page()
    {
        var commands = new RecordingAllocationsCommands();

        await CreateTools(commands).Scroll(page: "1:1048");

        Assert.Equal((null, new PageAddress(1, 1048)), commands.Scrolled);
    }

    [Fact]
    public async Task Allocations_Overlay_Without_A_Value_Turns_It_Off()
    {
        var commands = new RecordingAllocationsCommands { Overlay = AllocationOverlay.Gam };

        await CreateTools(commands).Overlay();

        Assert.Null(commands.Overlay);
    }

    [Fact]
    public async Task Allocations_Search_Without_Text_Clears_It()
    {
        var commands = new RecordingAllocationsCommands { Search = "x" };

        await CreateTools(commands).Search();

        Assert.Null(commands.Search);
    }

    private static AllocationCommandTools CreateTools(IAllocationsCommands commands)
        => new(new McpDocumentResolver(new TestAreaTab(McpArea.Allocations, commands: commands)));

    private sealed class RecordingAllocationsCommands : IAllocationsCommands
    {
        public (string?, bool)? Selected { get; private set; }

        public (string?, PageAddress?)? Scrolled { get; private set; }

        public AllocationOverlay? Overlay { get; set; }

        public string? Search { get; set; }

        public Task<string> SelectObjectAsync(string? name, bool add)
        {
            Selected = (name, add);

            return Task.FromResult("selected");
        }

        public Task<string> ScrollToAsync(string? name, PageAddress? page)
        {
            Scrolled = (name, page);

            return Task.FromResult("scrolled");
        }

        public Task<string> SetOverlayAsync(AllocationOverlay? overlay)
        {
            Overlay = overlay;

            return Task.FromResult("overlay");
        }

        public Task<string> SetSearchAsync(string? text)
        {
            Search = text;

            return Task.FromResult("search");
        }
    }
}