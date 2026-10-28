using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Mcp.Models.Context.Indexes;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context.Allocations;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools;
using InternalsViewer.Mcp.Tools.Allocations;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class AllocationAndNavigationToolsTests
{
    [Fact]
    public async Task Allocations_Objects_Filters_By_Name_And_Hides_System_Objects()
    {
        var tab = CreateAllocationsTab();

        var tools = new AllocationTools(new McpDocumentResolver(tab));

        var visible = await tools.Objects(null, false, null, 50, 0, null, CancellationToken.None);

        Assert.Contains("dbo.Orders.PK_Orders", visible);
        Assert.DoesNotContain("sys.sysallocunits", visible);

        var system = await tools.Objects("sysalloc", true, null, 50, 0, null, CancellationToken.None);

        Assert.Contains("sys.sysallocunits", system);
        Assert.DoesNotContain("dbo.Orders", system);
    }

    [Fact]
    public async Task Allocations_Files_Lists_The_Database_Files()
    {
        var tab = CreateAllocationsTab();

        var text = await new AllocationTools(new McpDocumentResolver(tab)).Files(null, CancellationToken.None);

        Assert.Contains("Sales_Data", text);
    }

    [Fact]
    public async Task Show_Page_From_An_Area_Tab_Opens_A_New_Tab_Through_The_Workspace()
    {
        var tab = new TestAreaTab(McpArea.Page) { Database = CreateDatabase() };

        var workspace = new RecordingWorkspace();

        var text = await new NavigationTools(new McpDocumentResolver(tab), workspace).ShowPage("1:336", 4);

        Assert.Contains("1:336", text);
        Assert.Equal([(new PageAddress(1, 336), (ushort?)4)], workspace.Pages);
    }

    [Fact]
    public async Task Show_Page_Without_A_Workspace_Fails_For_An_Area_Tab()
    {
        var tab = new TestAreaTab(McpArea.Page) { Database = CreateDatabase() };

        await Assert.ThrowsAsync<McpException>(() => new NavigationTools(new McpDocumentResolver(tab)).ShowPage("1:336"));
    }

    [Fact]
    public async Task Show_Index_Opens_The_Root_Page_Of_The_Allocation_Unit()
    {
        var tab = CreateAllocationsTab();

        var workspace = new RecordingWorkspace();

        await new NavigationTools(new McpDocumentResolver(tab), workspace).ShowIndex("PK_Orders");

        Assert.Equal([new PageAddress(1, 400)], workspace.Indexes);
    }

    [Fact]
    public async Task Get_Selection_Reports_An_Area_Tabs_Selection_Lines()
    {
        var node = new IndexNode(new PageAddress(1, 200)) { PageType = PageType.Data };

        var tab = new TestAreaTab(
            McpArea.Index,
            new IndexContext { TabId = "t1", Title = "Index: PK_Orders", Nodes = [node], SelectedNode = node });

        var text = await new DocumentTools(new McpDocumentResolver(tab)).GetSelection();

        Assert.Contains("Index tab t1", text);
        Assert.Contains("(1:200) Data height 0 selected", text);
    }

    private static TestAreaTab CreateAllocationsTab()
    {
        var database = CreateDatabase();

        var context = new AllocationsContext
        {
            TabId = "t1",
            Title = "Test",
            Database = database,
            Files = database.Files,
            AllocationUnits = database.AllocationUnits
        };

        return new TestAreaTab(McpArea.Allocations, context) { Database = database };
    }

    private static DatabaseSource CreateDatabase()
    {
        var database = new DatabaseSource(new TestConnection()) { Name = "Sales" };

        database.Files.Add(new DatabaseFile(1) { Name = "Sales_Data", PhysicalName = @"C:\Data\Sales.mdf", TotalPages = 1024 });

        database.AllocationUnits[1] = new AllocationUnit
        {
            AllocationUnitId = 1,
            SchemaName = "dbo",
            TableName = "Orders",
            IndexName = "PK_Orders",
            RootPage = new PageAddress(1, 400),
            FirstPage = new PageAddress(1, 300),
            DisplayName = "dbo.Orders"
        };

        database.AllocationUnits[2] = new AllocationUnit
        {
            AllocationUnitId = 2,
            SchemaName = "sys",
            TableName = "sysallocunits",
            IsSystem = true,
            DisplayName = "sys.sysallocunits"
        };

        return database;
    }

    private sealed class TestConnection : IConnectionType
    {
        public IPageReader PageReader => null!;

        public string Identifier => "test";

        public string Name { get; set; } = "test";

        public string GetConnectionString() => string.Empty;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingWorkspace : IMcpWorkspace
    {
        public List<(PageAddress Page, ushort? Slot)> Pages { get; } = [];

        public List<PageAddress> Indexes { get; } = [];

        public Task OpenPageAsync(DatabaseSource database, PageAddress pageAddress, ushort? slot, CancellationToken cancellationToken)
        {
            Pages.Add((pageAddress, slot));

            return Task.CompletedTask;
        }

        public Task OpenIndexAsync(DatabaseSource database, PageAddress rootPage, CancellationToken cancellationToken)
        {
            Indexes.Add(rootPage);

            return Task.CompletedTask;
        }

        public Task OpenColumnstoreAsync(DatabaseSource database, long allocationUnitId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
