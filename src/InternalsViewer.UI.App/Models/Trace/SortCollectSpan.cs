using InternalsViewer.Execution.AccessPaths.Results;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed record SortCollectSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public RowCountProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}
