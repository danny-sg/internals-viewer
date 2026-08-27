using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record ComputeVectorSpan() : AccessStep(AccessPhase.Compute), ITraceSpan
{
    public ComputeVectorProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class ComputeVectorProgress : ObservableObject
{
    [ObservableProperty]
    private string _columns = string.Empty;

    [ObservableProperty]
    private long _batches;

    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _total;

    public void Apply(AccessStep.ComputeVector step)
    {
        if (Columns != step.Columns)
        {
            Columns = step.Columns;
        }

        Batches = step.Number;

        Rows = step.RowCount;

        Total += step.RowCount;
    }
}
