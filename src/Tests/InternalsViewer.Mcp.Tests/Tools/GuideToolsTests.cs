using InternalsViewer.Mcp.Guides;
using InternalsViewer.Mcp.Interfaces.Guides;
using InternalsViewer.Mcp.Tools;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class GuideToolsTests
{
    [Fact]
    public void No_Topic_Lists_The_Guides_Built_In_First()
    {
        var text = new GuideTools([new EmbeddedGuideSource()]).Guide();

        var lines = text.Split(Environment.NewLine);

        Assert.Equal("Guides:", lines[0]);
        Assert.StartsWith("- reading:", lines[1]);
        Assert.StartsWith("- events:", lines[2]);
        Assert.StartsWith("- engine-symbols:", lines[3]);
        Assert.StartsWith("- full-trace-investigations:", lines[4]);
        Assert.StartsWith("- index-seek:", lines[5]);
        Assert.StartsWith("- index-scan:", lines[6]);
        Assert.StartsWith("- source:", lines[7]);
        Assert.Contains(lines, l => l.StartsWith("- deep-dive-full-trace: How Full Trace works"));
        Assert.Contains(lines, l => l.StartsWith("- deep-dive-query-tracing: How query tracing works"));
    }

    [Theory]
    [InlineData("reading", "Full Trace has no clock")]
    [InlineData("events", "# Event types")]
    [InlineData("engine-symbols", "## Reading a name")]
    [InlineData("full-trace-investigations", "## The method")]
    [InlineData("source", "github.com/danny-sg/internals-viewer")]
    [InlineData("deep-dive-operator-matching", "# How operator matching works")]
    public void Each_Guide_Returns_Its_Text(string topic, string expected)
    {
        var text = new GuideTools([new EmbeddedGuideSource()]).Guide(topic);

        Assert.Contains(expected, text);
    }

    [Fact]
    public void An_Unknown_Topic_Lists_The_Guides()
    {
        var text = new GuideTools([new EmbeddedGuideSource()]).Guide("plans");

        Assert.StartsWith("No guide plans.", text);
        Assert.Contains("- reading:", text);
    }

    [Fact]
    public void Guides_From_Another_Source_Are_Listed_And_Served()
    {
        var tools = new GuideTools([new EmbeddedGuideSource(), new AppSource()]);

        Assert.Contains("- app: The views", tools.Guide());
        Assert.Equal("# Timeline", tools.Guide("App"));
    }

    private sealed class AppSource : IMcpGuideSource
    {
        public IEnumerable<McpGuide> GetGuides() => [new McpGuide("app", "The views", () => "# Timeline")];
    }
}
