using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record BatchCountSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public RowCountProgress Progress { get; } = new();

    public string Badge { get; init; } = string.Empty;

    public bool IsComplete { get; set; }
}
