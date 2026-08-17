using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record RankSpan() : AccessStep(AccessPhase.Rank), ITraceSpan
{
    public RankProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class RankProgress : ObservableObject
{
    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _partitions;

    [ObservableProperty]
    private string _values = "";

    public void Apply(AccessStep.RankRow step)
    {
        Rows = step.Number;
        Values = step.Values;

        if (step.IsNewPartition)
        {
            Partitions++;
        }
    }
}
