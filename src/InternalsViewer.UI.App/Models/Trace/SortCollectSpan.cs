using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed record SortCollectSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public RowCountProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}
