using InternalsViewer.UI.App.Services.Mcp;

namespace InternalsViewer.UI.App.Tests.Services.Mcp;

[Trait("Category", "Unit")]
public class InstructionsGuideSourceTests
{
    [Fact]
    public void Flatten_Turns_Ticks_And_App_Links_Into_Plain_Names()
    {
        const string markdown = "- [{{ShowWaits}}] [Waits](option:ShowWaits) - where the query waited\n" +
                                "The [Timeline](view:Timeline) replays it, see [Call Stack](guide:CallStack).";

        var text = InstructionsGuideSource.Flatten(markdown);

        Assert.Equal("- **Waits** - where the query waited\nThe **Timeline** replays it, see **Call Stack**.", text);
    }

    [Fact]
    public void The_App_Guide_Holds_Every_Instruction_Page_Without_App_Markup()
    {
        var guide = Assert.Single(new InstructionsGuideSource().GetGuides());

        var text = guide.Read();

        Assert.Equal("app", guide.Topic);
        Assert.Contains("# Timeline", text);
        Assert.Contains("# Full Trace", text);
        Assert.Contains("# Simulation", text);
        Assert.DoesNotContain("{{", text);
        Assert.DoesNotContain("](view:", text);
        Assert.DoesNotContain("](option:", text);
        Assert.DoesNotContain("](guide:", text);
    }
}
