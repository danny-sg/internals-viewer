using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace;

public sealed record FullTraceResult(CallStackTree CallStack, FullTraceCallLog CallLog, FullTraceTimeline Timeline)
{
    public IteratorPlanLink PlanLink { get; init; } = IteratorPlanLink.Empty;
}
