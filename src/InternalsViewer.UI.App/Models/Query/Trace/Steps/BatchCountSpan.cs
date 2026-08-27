using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record BatchCountSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public RowCountProgress Progress { get; } = new();

    public BatchWorkProgress Work { get; } = new();

    public string Badge { get; init; } = string.Empty;

    public bool IsComplete { get; set; }
}

public sealed partial class BatchWorkProgress : ObservableObject
{
    [ObservableProperty]
    private long _rleEntries;

    [ObservableProperty]
    private long _operations;

    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _qualifying;

    [ObservableProperty]
    private long _materialised;

    [ObservableProperty]
    private bool _hasFilter;

    public void Apply(AccessStep.BatchProduced step)
    {
        HasFilter = step.HasCompressedFilter;

        Materialised += step.Materialised;

        RleEntries += step.FilterRleEntries;

        Operations += step.FilterOperations;

        Rows += step.RowCount;

        Qualifying += step.QualifyingCount;
    }
}
