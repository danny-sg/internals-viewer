using InternalsViewer.Mcp.Prompts;

namespace InternalsViewer.Mcp.Tests.Prompts;

[Trait("Category", "Unit")]
public class ServerInstructionsTests
{
    private const int ClientLimit = 3_500;

    [Fact]
    public void The_Instructions_Fit_Before_Clients_Cut_Them_Off()
        => Assert.InRange(ServerInstructions.Text.Length, 1, ClientLimit);

    [Theory]
    [InlineData("a learning tool")]
    [InlineData("It is not a tuning tool")]
    [InlineData("Never report a Full Trace figure as milliseconds")]
    [InlineData("Ask before close_tab")]
    [InlineData("offer to point things out on the Trace Timeline")]
    [InlineData("IMPORTANT RESPONSE BEHAVIOUR")]
    public void The_Rules_That_Always_Apply_Are_In_The_Instructions(string rule)
        => Assert.Contains(rule, ServerInstructions.Text);
}
