using InternalsViewer.Internals.Engine.Loading;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Loading;

[Trait("Category", "Unit")]
[Trait("Area", "Loading")]
public class StepProgressTests
{
    private static readonly ProgressStep[] Steps = [new("Opening", 25), new("Replaying", 50), new("Building", 25)];

    [Fact]
    public void A_Step_Starts_Indeterminate()
    {
        var reported = new List<ProgressDetail>();

        new StepProgress(new Collector(reported), Steps).Begin("Opening");

        var detail = Assert.Single(reported);

        Assert.Equal(("Opening", true, true, 0d), (detail.Message, detail.IsStep, detail.IsIndeterminate, detail.Overall));
    }

    [Fact]
    public void A_Finished_Step_Reports_How_Long_It_Took_And_Adds_Its_Weight()
    {
        var reported = new List<ProgressDetail>();

        var steps = new StepProgress(new Collector(reported), Steps);

        steps.Begin("Opening");
        steps.Complete();

        var finished = reported[^1];

        Assert.NotNull(finished.Elapsed);
        Assert.Equal(25d, finished.Overall);
    }

    [Fact]
    public void A_Measured_Step_Moves_The_Overall_By_Its_Share()
    {
        var reported = new List<ProgressDetail>();

        var steps = new StepProgress(new Collector(reported), Steps);

        steps.Begin("Opening");
        steps.Complete();
        steps.Begin("Replaying");
        steps.Report(50);

        Assert.Equal(("Replaying", 50d, 50d), (reported[^1].Message, reported[^1].Percentage, reported[^1].Overall));
    }

    [Fact]
    public void Every_Step_Finished_Is_The_Whole()
    {
        var steps = new StepProgress(null, Steps);

        foreach (var step in Steps)
        {
            steps.Begin(step.Name);
            steps.Complete();
        }

        Assert.Equal(100d, steps.Overall);
    }

    [Fact]
    public void A_Percentage_Outside_The_Step_Is_Clamped()
    {
        var steps = new StepProgress(null, Steps);

        steps.Begin("Opening");
        steps.Report(250);

        Assert.Equal(25d, steps.Overall);
    }

    private sealed class Collector(List<ProgressDetail> reported) : IProgress<ProgressDetail>
    {
        public void Report(ProgressDetail value) => reported.Add(value);
    }
}
