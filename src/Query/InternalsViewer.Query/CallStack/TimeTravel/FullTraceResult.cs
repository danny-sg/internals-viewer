using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record FullTraceResult(CallStackTree CallStack, TimeTravelCallLog CallLog, TimeTravelTimeline Timeline)
{
    public IteratorPlanLink PlanLink { get; init; } = IteratorPlanLink.Empty;
}
