using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Models.Context.Indexes;
using InternalsViewer.Mcp.Prompts.Indexes;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Indexes;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class IndexRecordsToolsTests
{
    [Fact]
    public async Task Index_Records_Lists_Keys_And_Child_Pages()
    {
        var context = new IndexContext
        {
            TabId = "i1",
            Title = "Test",
            SelectedNode = new IndexNode(new PageAddress(1, 384)),
            Records =
            [
                new IndexRecordContext(0, "1:376", ["Id=NULL"]),
                new IndexRecordContext(1, "1:392", ["Id=171"])
            ]
        };

        var tools = new IndexTools(new McpDocumentResolver(new TestAreaTab(McpArea.Index, context)));

        var text = await tools.Records();

        Assert.Contains("Page (1:384)", text);
        Assert.Contains("slot 1: Id=171 -> 1:392", text);
    }

    [Fact]
    public async Task Index_Records_Without_A_Selected_Page_Asks_For_One()
    {
        var tools = new IndexTools(new McpDocumentResolver(new TestAreaTab(McpArea.Index, new IndexContext { TabId = "i1", Title = "Test" })));

        var exception = await Assert.ThrowsAsync<McpException>(() => tools.Records());

        Assert.Contains("index_select_page", exception.Message);
    }

    [Fact]
    public void Index_Scan_Prompt_Follows_The_Direction()
    {
        var descending = IndexPrompts.Scan("dbo.ClusteredTable", "descending");

        Assert.Contains("right edge", descending);
        Assert.Contains("previous page", descending);
        Assert.Contains("left edge", IndexPrompts.Scan());
    }

    [Fact]
    public void Index_Seek_Prompt_Names_The_Key_And_The_Guide()
    {
        var text = IndexPrompts.Seek("1000", "dbo.ClusteredTable");

        Assert.Contains("1000", text);
        Assert.Contains("dbo.ClusteredTable", text);
        Assert.Contains("index-seek", text);
    }
}
