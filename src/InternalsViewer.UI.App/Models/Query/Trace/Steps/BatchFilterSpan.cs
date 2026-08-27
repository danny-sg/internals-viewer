using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record BatchFilterSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public BatchFilterProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class BatchFilterProgress : ObservableObject
{
    [ObservableProperty]
    private long _batches;

    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _selected;

    [ObservableProperty]
    private long _passed;

    [ObservableProperty]
    private string _columns = "";

    public void Apply(AccessStep.FilterVector step)
    {
        Batches = step.Number;

        Rows += step.RowsEvaluated;

        if (Columns != step.Columns)
        {
            Columns = step.Columns;
        }
    }

    public void Apply(AccessStep.BatchFiltered step)
    {
        Batches = step.Number;

        Selected = step.QualifyingCount;

        Passed = step.PassedCount;
    }
}
