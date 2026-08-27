using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record BatchGetSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public BatchGetProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class BatchGetProgress : ObservableObject
{
    [ObservableProperty]
    private long _batches;

    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _passed;

    public void Apply(AccessStep.BatchFiltered step)
    {
        Batches = step.Number;

        Rows += step.RowCount;

        Passed = step.PassedCount;
    }
}

public sealed record BatchFilterSpan() : AccessStep(AccessPhase.Filter), ITraceSpan
{
    public BatchFilterProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class BatchFilterProgress : ObservableObject
{
    [ObservableProperty]
    private string _columns = "";

    [ObservableProperty]
    private long _evaluated;

    [ObservableProperty]
    private long _selected;

    public void Apply(AccessStep.FilterVector step)
    {
        if (Columns != step.Columns)
        {
            Columns = step.Columns;
        }

        Evaluated = step.RowsEvaluated;

        Selected = step.Matches;
    }
}
