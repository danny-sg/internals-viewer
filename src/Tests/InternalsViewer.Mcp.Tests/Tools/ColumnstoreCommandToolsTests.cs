using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Columnstore;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Columnstore;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class ColumnstoreCommandToolsTests
{
    private static ColumnstoreContext Context => new()
    {
        TabId = "t1",
        Title = "Test",
        IsLoaded = true,
        Documents = ["Structure", "Metadata", "Segment 2:5"],
        CurrentDocument = "Segment 2:5",
        AvailableRegions = ["Header", "Handles"],
        MarkerNames = ["Header Size", "Entry Count"]
    };

    [Fact]
    public async Task Open_Passes_The_Item_And_Ids_And_Reports_The_Current_Document()
    {
        var (tools, commands) = Create();

        var text = await tools.Open(ColumnstoreItem.Segment, 2, 5);

        Assert.Equal((ColumnstoreItem.Segment, 2, 5, null), commands.Opened);
        Assert.Equal("Opened Segment 2:5.", text);
    }

    [Fact]
    public async Task Open_Needs_The_Ids_For_The_Item()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.Open(ColumnstoreItem.Segment, 2));
        await Assert.ThrowsAsync<McpException>(() => tools.Open(ColumnstoreItem.Dictionary));
        await Assert.ThrowsAsync<McpException>(() => tools.Open(ColumnstoreItem.DeltaStore));

        Assert.Null(commands.Opened);
    }

    [Fact]
    public async Task Open_Waits_For_The_Index_To_Load()
    {
        var (tools, commands) = Create(Context with { IsLoaded = false });

        await Assert.ThrowsAsync<McpException>(() => tools.Open(ColumnstoreItem.DeleteBitmap));

        Assert.Null(commands.Opened);
    }

    [Fact]
    public async Task Show_Rejects_A_Document_That_Is_Not_Open()
    {
        var (tools, commands) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() => tools.Show("Dictionary"));

        Assert.Contains("Metadata", exception.Message);
        Assert.Empty(commands.Calls);
    }

    [Fact]
    public async Task Show_And_Metadata_Tab_Reach_The_Tab()
    {
        var (tools, commands) = Create();

        await tools.Show("metadata");
        await tools.MetadataTab(ColumnstoreMetadataTab.Dictionaries);

        Assert.Equal(["metadata", "Dictionaries"], commands.Calls);
    }

    [Fact]
    public async Task Region_Rejects_A_Region_The_Document_Does_Not_Have()
    {
        var (tools, commands) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() => tools.Region("Entries"));

        Assert.Contains("Handles", exception.Message);
        Assert.Empty(commands.Calls);
    }

    [Fact]
    public async Task Region_Needs_A_Segment_Or_Dictionary_To_Be_Open()
    {
        var (tools, commands) = Create(Context with { AvailableRegions = [] });

        await Assert.ThrowsAsync<McpException>(() => tools.Region("Header"));

        Assert.Empty(commands.Calls);
    }

    [Fact]
    public async Task Region_Reports_The_Region_The_Tab_Shows()
    {
        var (tools, _) = Create();

        var text = await tools.Region("handles");

        Assert.Equal("Showing the Handles region.", text);
    }

    [Fact]
    public async Task Select_Marker_Rejects_A_Name_Not_In_The_Context()
    {
        var (tools, commands) = Create();

        await Assert.ThrowsAsync<McpException>(() => tools.SelectMarker("nope"));

        Assert.False(commands.MarkerSelected);
    }

    [Fact]
    public async Task Select_Marker_Reports_The_Offset_And_Length_The_Tab_Selected()
    {
        var (tools, _) = Create();

        var text = await tools.SelectMarker("entry");

        Assert.Equal("Selected Entry Count at offset 12, 4 bytes.", text);
    }

    [Fact]
    public async Task Select_Marker_And_Row_Without_A_Target_Clear_The_Selection()
    {
        var (tools, commands) = Create(Context with { SelectedMarker = "Header Size", SelectedRow = 3 });

        Assert.Equal("Cleared the marker selection.", await tools.SelectMarker());
        Assert.Equal("Cleared the row selection.", await tools.SelectRow());
        Assert.True(commands.RowCleared);
    }

    [Fact]
    public async Task Select_Row_Reports_The_Row_The_Tab_Selected_And_Rejects_A_Negative_Index()
    {
        var (tools, _) = Create();

        Assert.Equal("Selected row 7 of Segment 2:5.", await tools.SelectRow(7));

        await Assert.ThrowsAsync<McpException>(() => tools.SelectRow(-1));
    }

    private static (ColumnstoreCommandTools Tools, RecordingColumnstoreCommands Commands) Create(ColumnstoreContext? context = null)
    {
        var tab = new TestAreaTab(McpArea.Columnstore, (context ?? Context) with { TabId = "t1", Title = "Test" });

        var commands = new RecordingColumnstoreCommands(tab);

        tab.Commands = commands;

        return (new ColumnstoreCommandTools(new McpDocumentResolver(tab)), commands);
    }

    private sealed class RecordingColumnstoreCommands(TestAreaTab tab) : IColumnstoreCommands
    {
        public (ColumnstoreItem, int?, int?, int?)? Opened { get; private set; }

        public bool MarkerSelected { get; private set; }

        public bool RowCleared { get; private set; }

        public List<string> Calls { get; } = [];

        private ColumnstoreContext Context => (ColumnstoreContext)tab.Context!;

        public Task OpenAsync(ColumnstoreItem item, int? rowGroup, int? column, int? dictionary)
        {
            Opened = (item, rowGroup, column, dictionary);

            return Task.CompletedTask;
        }

        public Task ShowDocumentAsync(string name)
        {
            Calls.Add(name);

            return Task.CompletedTask;
        }

        public Task ShowMetadataTabAsync(ColumnstoreMetadataTab list)
        {
            Calls.Add(list.ToString());

            tab.Context = Context with { MetadataTab = list };

            return Task.CompletedTask;
        }

        public Task ShowRegionAsync(string region)
        {
            Calls.Add(region);

            var match = Context.AvailableRegions.First(r => r.Equals(region, StringComparison.OrdinalIgnoreCase));

            tab.Context = Context with { Region = match };

            return Task.CompletedTask;
        }

        public Task SelectMarkerAsync(string? name)
        {
            MarkerSelected = name is not null;

            tab.Context = Context with
            {
                SelectedMarker = name is null ? null : Context.MarkerNames.First(n => n.Contains(name, StringComparison.OrdinalIgnoreCase)),
                SelectedMarkerOffset = 12,
                SelectedMarkerLength = 4
            };

            return Task.CompletedTask;
        }

        public Task SelectRowAsync(int? index)
        {
            RowCleared = index is null;

            tab.Context = Context with { SelectedRow = index };

            return Task.CompletedTask;
        }
    }
}
