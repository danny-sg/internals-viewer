using InternalsViewer.UI.App.Controls.Query.Instructions;

namespace InternalsViewer.UI.App.Tests.Controls.Query.Instructions;

[Trait("Category", "Unit")]
public class InstructionsLinkTests
{
    [Fact]
    public void Parses_A_Guide_Link_As_A_Page()
    {
        var result = InstructionsLink.TryParse(new Uri("guide:ChoosingEvents"), out var link);

        Assert.True(result);
        Assert.Equal(InstructionsLinkType.Page, link.Type);
        Assert.Equal("ChoosingEvents", link.Target);
    }

    [Fact]
    public void Parses_An_Option_Link_As_A_Toggle_Preserving_Case()
    {
        var result = InstructionsLink.TryParse(new Uri("option:ShowLatches"), out var link);

        Assert.True(result);
        Assert.Equal(InstructionsLinkType.ToggleOption, link.Type);
        Assert.Equal("ShowLatches", link.Target);
    }

    [Fact]
    public void Parses_A_View_Link_As_An_Open_View()
    {
        var result = InstructionsLink.TryParse(new Uri("view:Allocations"), out var link);

        Assert.True(result);
        Assert.Equal(InstructionsLinkType.OpenView, link.Type);
        Assert.Equal("Allocations", link.Target);
    }

    [Fact]
    public void Parses_An_Https_Link_As_External_With_The_Full_Uri()
    {
        var result = InstructionsLink.TryParse(new Uri("https://internalsviewer.com/docs"), out var link);

        Assert.True(result);
        Assert.Equal(InstructionsLinkType.External, link.Type);
        Assert.Equal("https://internalsviewer.com/docs", link.Target);
    }

    [Fact]
    public void Rejects_A_Null_Uri()
    {
        Assert.False(InstructionsLink.TryParse(null, out _));
    }

    [Fact]
    public void Rejects_A_Relative_Uri()
    {
        Assert.False(InstructionsLink.TryParse(new Uri("Overview.md", UriKind.Relative), out _));
    }

    [Fact]
    public void Rejects_An_Unknown_Scheme()
    {
        Assert.False(InstructionsLink.TryParse(new Uri("mailto:someone@example.com"), out _));
    }
}
