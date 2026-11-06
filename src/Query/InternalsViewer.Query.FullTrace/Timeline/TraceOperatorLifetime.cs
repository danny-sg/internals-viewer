using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceOperatorLifetime(uint Thread,
                                           ExecutionOperatorEvent Operator,
                                           int CallNodeIndex,
                                           IReadOnlyList<TraceSpan> Calls,
                                           MemoryUsageCurve InUse,
                                           TraceScale Scale)
{
    public TracePoint Constructed { get; init; } = Calls[0].From;

    public TracePoint Destroyed { get; init; } = Calls[^1].To;

    public IReadOnlyList<ulong> Instances { get; init; } = [];

    public TracePoint Start => Calls[0].From;

    public TracePoint End => Calls[^1].To;

    public double GetStart(TraceTimelineAxis axis) => Scale.GetValue(Start, axis);

    public double GetEnd(TraceTimelineAxis axis) => Scale.GetValue(End, axis);

    public double GetConstructed(TraceTimelineAxis axis) => Scale.GetValue(Constructed, axis);

    public double GetDestroyed(TraceTimelineAxis axis) => Scale.GetValue(Destroyed, axis);
}
