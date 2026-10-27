using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Models.Context.Columnstore;
using InternalsViewer.Mcp.Models.Context.Indexes;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Columnstore;
using InternalsViewer.Mcp.Tools.Indexes;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class AreaToolsTests
{
    [Fact]
    public async Task Index_Structure_Summarises_Levels_And_Out_Of_Order_Leaves()
    {
        var root = new IndexNode(new PageAddress(1, 100)) { Level = 0, IndexLevel = 1, Ordinal = 1, SlotCount = 2, PageType = PageType.Index };

        var first = new IndexNode(new PageAddress(1, 200))
        {
            Level = 1, IndexLevel = 0, Ordinal = 1, SlotCount = 50, PageType = PageType.Data, Parent = root.PageAddress,
            NextPage = new PageAddress(1, 201)
        };

        var second = new IndexNode(new PageAddress(1, 201))
        {
            Level = 1, IndexLevel = 0, Ordinal = 2, SlotCount = 40, PageType = PageType.Data, Parent = root.PageAddress,
            NextPage = new PageAddress(1, 900), PreviousPage = first.PageAddress
        };

        var tab = new TestAreaTab(McpArea.Index, new IndexContext { TabId = "t1", Title = "Test", Nodes = [root, first, second] });

        var text = await new IndexTools(new McpDocumentResolver(tab)).Structure(null, CancellationToken.None);

        Assert.Contains("3 pages", text);
        Assert.Contains("Data 2", text);
        Assert.Contains("1 of 2 leaf pages are not followed", text);
    }

    [Fact]
    public async Task Index_Nodes_Filters_By_Height()
    {
        var root = new IndexNode(new PageAddress(1, 100)) { IndexLevel = 1, Ordinal = 1 };

        var leaf = new IndexNode(new PageAddress(1, 200)) { Level = 1, IndexLevel = 0, Ordinal = 1, Parent = root.PageAddress };

        var tab = new TestAreaTab(McpArea.Index, new IndexContext { TabId = "t1", Title = "Test", Nodes = [root, leaf] });

        var text = await new IndexTools(new McpDocumentResolver(tab)).Nodes(0, null, 50, 0, null, CancellationToken.None);

        Assert.Contains("1 pages", text);
        Assert.Contains("(1:200)", text);
        Assert.Single(text.Split('\n'), l => l.StartsWith("(1:") || l.TrimStart().StartsWith("(1:"));
    }

    [Fact]
    public async Task Index_Tools_Reject_A_Tab_Of_Another_Area()
    {
        var tab = new TestAreaTab(McpArea.Page) { Id = "p1" };

        await Assert.ThrowsAsync<McpException>(
            () => new IndexTools(new McpDocumentResolver(tab)).Structure("p1", CancellationToken.None));
    }

    [Fact]
    public async Task Columnstore_RowGroups_Lists_State_And_Rows()
    {
        var index = new ColumnStoreIndex { SchemaName = "dbo", TableName = "Facts", IndexName = "CCI", IsClustered = true };

        index.RowGroups.Add(new RowGroup { RowGroupId = 0, State = RowGroupState.Compressed, TotalRows = 1048576 });
        index.RowGroups.Add(new RowGroup { RowGroupId = 1, State = RowGroupState.Open, TotalRows = 10 });

        var tab = new TestAreaTab(McpArea.Columnstore, new ColumnstoreContext { TabId = "t1", Title = "Test", Index = index });

        var tools = new ColumnstoreTools(new McpDocumentResolver(tab));

        var all = await tools.RowGroups(null, 50, 0, null, CancellationToken.None);

        Assert.Contains("2 row groups", all);
        Assert.Contains("1,048,576", all);

        var open = await tools.RowGroups("open", 50, 0, null, CancellationToken.None);

        Assert.Contains("Open", open);
        Assert.DoesNotContain("Compressed", open);
    }
}
