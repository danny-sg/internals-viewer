using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record StreamAggregateSpan() : AccessStep(AccessPhase.Accumulate), ITraceSpan
{
    public AggregateProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class AggregateProgress : ObservableObject
{
    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _groupRows;

    [ObservableProperty]
    private string _running = "";

    public void Apply(AccessStep.AggregateRow step)
    {
        Rows = step.Number;
        GroupRows = step.GroupRows;
        Running = step.Running;
    }

    public void Apply(AccessStep.HashAggregate step)
    {
        Rows = step.Number;
        GroupRows = step.GroupRows;
        Running = step.Running;
    }
}
