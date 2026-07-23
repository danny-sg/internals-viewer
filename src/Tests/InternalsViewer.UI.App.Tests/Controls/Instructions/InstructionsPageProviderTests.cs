using InternalsViewer.UI.App.Controls.Instructions;

namespace InternalsViewer.UI.App.Tests.Controls.Instructions;

public class InstructionsPageProviderTests
{
    [Fact]
    public void Loads_The_Overview_Page_From_Embedded_Resources()
    {
        var markdown = InstructionsPageProvider.GetPage("Overview");

        Assert.NotNull(markdown);
        Assert.Contains("# Query view", markdown);
    }

    [Fact]
    public void Loads_The_Choosing_Events_Page_From_Embedded_Resources()
    {
        var markdown = InstructionsPageProvider.GetPage("ChoosingEvents");

        Assert.NotNull(markdown);
        Assert.Contains("option:ShowLatches", markdown);
    }

    [Fact]
    public void Returns_Null_For_An_Unknown_Page()
    {
        Assert.Null(InstructionsPageProvider.GetPage("NotAPage"));
    }

    [Fact]
    public void Renders_A_True_Token_As_A_Ticked_Task_Item()
    {
        var rendered = InstructionsPageProvider.Render("- [{{ShowLatches}}] Latches",
                                                       new Dictionary<string, bool> { ["ShowLatches"] = true });

        Assert.Equal("- [x] Latches", rendered);
    }

    [Fact]
    public void Renders_A_False_Token_As_An_Unticked_Task_Item()
    {
        var rendered = InstructionsPageProvider.Render("- [{{ShowLatches}}] Latches",
                                                       new Dictionary<string, bool> { ["ShowLatches"] = false });

        Assert.Equal("- [ ] Latches", rendered);
    }

    [Fact]
    public void Leaves_An_Unknown_Token_Unchanged()
    {
        var rendered = InstructionsPageProvider.Render("- [{{Nope}}] Latches", new Dictionary<string, bool>());

        Assert.Equal("- [{{Nope}}] Latches", rendered);
    }
}
