using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Columnstore;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Columnstore;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class ColumnstoreCommandToolsTests
{
    [Fact]
    public async Task Open_Passes_The_Item_And_Ids()
    {
        var commands = new RecordingColumnstoreCommands();

        await CreateTools(commands).Open(ColumnstoreItem.Segment, 2, 5);

        Assert.Equal((ColumnstoreItem.Segment, 2, 5, null), commands.Opened);
    }

    [Fact]
    public async Task Select_Marker_And_Row_Without_A_Target_Clear_The_Selection()
    {
        var commands = new RecordingColumnstoreCommands();

        var tools = CreateTools(commands);

        await tools.SelectMarker();
        await tools.SelectRow();

        Assert.True(commands.MarkerCleared);
        Assert.True(commands.RowCleared);
    }

    [Fact]
    public async Task Region_And_Show_Reach_The_Tab()
    {
        var commands = new RecordingColumnstoreCommands();

        var tools = CreateTools(commands);

        await tools.Region("Handles");
        await tools.Show("Metadata");
        await tools.MetadataTab(ColumnstoreMetadataTab.Dictionaries);

        Assert.Equal(["Handles", "Metadata", "Dictionaries"], commands.Calls);
    }

    private static ColumnstoreCommandTools CreateTools(IColumnstoreCommands commands)
        => new(new McpDocumentResolver(new TestAreaTab(McpArea.Columnstore, commands: commands)));

    private sealed class RecordingColumnstoreCommands : IColumnstoreCommands
    {
        public (ColumnstoreItem, int?, int?, int?)? Opened { get; private set; }

        public bool MarkerCleared { get; private set; }

        public bool RowCleared { get; private set; }

        public List<string> Calls { get; } = [];

        public Task<string> OpenAsync(ColumnstoreItem item, int? rowGroup, int? column, int? dictionary)
        {
            Opened = (item, rowGroup, column, dictionary);

            return Task.FromResult("opened");
        }

        public Task<string> ShowDocumentAsync(string name)
        {
            Calls.Add(name);

            return Task.FromResult("shown");
        }

        public Task<string> ShowMetadataTabAsync(ColumnstoreMetadataTab tab)
        {
            Calls.Add(tab.ToString());

            return Task.FromResult("metadata");
        }

        public Task<string> ShowRegionAsync(string region)
        {
            Calls.Add(region);

            return Task.FromResult("region");
        }

        public Task<string> SelectMarkerAsync(string? name)
        {
            MarkerCleared = name is null;

            return Task.FromResult("marker");
        }

        public Task<string> SelectRowAsync(int? index)
        {
            RowCleared = index is null;

            return Task.FromResult("row");
        }
    }
}