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

    public void Apply(long rleEntries, long operations, long rows, long qualifying)
    {
        RleEntries += rleEntries;

        Operations += operations;

        Rows += rows;

        Qualifying += qualifying;
    }
}
