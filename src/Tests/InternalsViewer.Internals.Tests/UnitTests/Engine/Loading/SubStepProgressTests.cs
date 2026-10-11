using InternalsViewer.Internals.Engine.Loading;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Loading;

[Trait("Category", "Unit")]
[Trait("Area", "Loading")]
public class SubStepProgressTests
{
    [Fact]
    public void A_Sub_Stage_Percentage_Moves_The_Current_Step()
    {
        var reported = new List<ProgressDetail>();

        var steps = new StepProgress(new Collector(reported), [new ProgressStep("Opening Database", 50), new ProgressStep("Loading", 50)]);

        steps.Begin("Opening Database");

        new SubStepProgress(steps, NullLogger.Instance).Report(new ProgressDetail("Mapping pages", 40));

        Assert.Equal(("Opening Database", 40d, 20d), (reported[^1].Message, reported[^1].Percentage, reported[^1].Overall));
    }

    [Fact]
    public void A_Sub_Stage_Message_Is_Not_Shown_As_A_Step()
    {
        var reported = new List<ProgressDetail>();

        var steps = new StepProgress(new Collector(reported), [new ProgressStep("Opening Database", 100)]);

        steps.Begin("Opening Database");

        new SubStepProgress(steps, NullLogger.Instance).Report(new ProgressDetail("Opening backup.bak"));

        Assert.DoesNotContain(reported, r => r.Message == "Opening backup.bak");
    }

    private sealed class Collector(List<ProgressDetail> reported) : IProgress<ProgressDetail>
    {
        public void Report(ProgressDetail value) => reported.Add(value);
    }
}
