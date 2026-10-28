using InternalsViewer.Mcp.Prompts;

namespace InternalsViewer.Mcp.Tests.Prompts;

[Trait("Category", "Unit")]
public class PromptTemplateTests
{
    [Fact]
    public void Render_Fills_Placeholders_From_Properties()
    {
        var text = PromptTemplate.Render("Find {{key}} in {{target}}.", new { key = "1000", target = "the index" });

        Assert.Equal("Find 1000 in the index.", text);
    }

    [Fact]
    public void Render_Throws_For_A_Placeholder_Without_A_Value()
    {
        Assert.Throws<InvalidOperationException>(() => PromptTemplate.Render("Find {{key}}.", new { other = "x" }));
    }

    [Theory]
    [InlineData("server-instructions")]
    [InlineData("explore")]
    [InlineData("explore-visible")]
    [InlineData("explore-selection")]
    [InlineData("index-seek")]
    [InlineData("index-scan")]
    [InlineData("index-scan-ascending")]
    [InlineData("index-scan-descending")]
    public void Every_Prompt_Text_Is_Embedded(string name)
    {
        Assert.NotEmpty(PromptStore.Get(name));
    }
}
