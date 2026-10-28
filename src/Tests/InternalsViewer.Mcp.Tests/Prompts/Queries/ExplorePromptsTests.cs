using InternalsViewer.Mcp.Prompts.Queries;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Prompts.Queries;

[Trait("Category", "Unit")]
public class ExplorePromptsTests
{
    [Fact]
    public void The_Default_Scope_Covers_The_Run()
    {
        var text = ExplorePrompts.Explore();

        Assert.Contains("Explain what SQL Server did", text);
        Assert.DoesNotContain("Scope the investigation", text);
    }

    [Fact]
    public void The_Visible_Scope_Passes_The_Ranges_In_Each_Axis()
    {
        var text = ExplorePrompts.Explore("why so many reads", "visible");

        Assert.Contains("why so many reads", text);
        Assert.Contains("fromMs and toMs", text);
        Assert.Contains("positions as hex", text);
        Assert.Contains("never milliseconds", text);
    }

    [Fact]
    public void The_Selection_Scope_Works_From_The_Selected_Item()
    {
        var text = ExplorePrompts.Explore(null, "Selection");

        Assert.Contains("calltree with operator or root", text);
    }

    [Fact]
    public void An_Unknown_Scope_Is_Refused()
    {
        Assert.Throws<McpException>(() => ExplorePrompts.Explore(null, "page"));
    }
}
