using System.Diagnostics;

namespace InternalsViewer.Internals.Engine.Loading;

public sealed class StepProgress(IProgress<ProgressDetail>? progress, IReadOnlyList<ProgressStep> steps) : IProgress<double>
{
    public double Overall
        => Total <= 0 ? 0 : Math.Clamp((Completed + (Current?.Weight ?? 0) * (Percentage ?? 0) / 100) / Total * 100, 0, 100);

    private double Total { get; } = steps.Sum(s => s.Weight);

    private double Completed { get; set; }

    private ProgressStep? Current { get; set; }

    private double? Percentage { get; set; }

    private long Started { get; set; }

    public void Begin(string name)
    {
        Current = steps.First(s => s.Name == name);
        Percentage = null;
        Started = Stopwatch.GetTimestamp();

        Send();
    }

    public void Report(double value)
    {
        if (Current is null)
        {
            return;
        }

        Percentage = Math.Clamp(value, 0, 100);

        Send();
    }

    public void Complete()
    {
        if (Current is not { } step)
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(Started);

        Completed += step.Weight;
        Current = null;
        Percentage = null;

        progress?.Report(new ProgressDetail(step.Name) { IsStep = true, Elapsed = elapsed, Overall = Overall });
    }

    private void Send()
    {
        if (Current is { } step)
        {
            progress?.Report(new ProgressDetail(step.Name, Percentage) { IsStep = true, Overall = Overall });
        }
    }
}
