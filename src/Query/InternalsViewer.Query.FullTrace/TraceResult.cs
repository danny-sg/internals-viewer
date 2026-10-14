using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace;

public sealed record TraceResult(CallStackTree CallStack, TraceCallLog CallLog, TraceTimeline Timeline) : IRunExtra
{
    public IteratorPlanLink PlanLink { get; init; } = IteratorPlanLink.Empty;

    public HoBtAccessMap HoBtAccess { get; init; } = HoBtAccessMap.Empty;

    public ObjectMap Objects { get; init; } = ObjectMap.Empty;

    public IReadOnlyList<CallStackNode> QueryRoots => Roots ??= QueryExecutionCropper.GetRoots(CallStack);

    public TraceTimeline QueryTimeline => CroppedTimeline ??= GetQueryTimeline();

    private IReadOnlyList<CallStackNode>? Roots { get; set; }

    private TraceTimeline? CroppedTimeline { get; set; }

    private TraceTimeline GetQueryTimeline()
        => QueryRoots.Count > 0
            ? Timeline.CroppedTo(QueryRoots.ToHashSet<CallStackNode>(ReferenceEqualityComparer.Instance)) ?? Timeline
            : Timeline;
}
