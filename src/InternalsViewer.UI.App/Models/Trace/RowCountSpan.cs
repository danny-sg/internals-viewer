using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed record RowCountSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public RowCountProgress Progress { get; } = new();

    public string Badge { get; init; } = "";

    public bool IsComplete { get; set; }
}

public sealed partial class RowCountProgress : ObservableObject
{
    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _limit;

    [ObservableProperty]
    private string _limitText = "";

    public void Apply(long number, long limit)
    {
        Rows = number;

        if (Limit != limit)
        {
            Limit = limit;

            LimitText = limit > 0 ? $"of {limit:N0}" : "";
        }
    }
}
