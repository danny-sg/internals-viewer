using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceOperatorLifetime(uint Thread,
                                           ExecutionOperatorEvent Operator,
                                           int Node,
                                           IReadOnlyList<TraceSpan> Calls,
                                           MemoryInUseCurve InUse,
                                           TraceScale Scale)
{
    public TracePoint Constructed { get; init; } = Calls[0].From;

    public TracePoint Destroyed { get; init; } = Calls[^1].To;

    public TracePoint Start => Calls[0].From;

    public TracePoint End => Calls[^1].To;

    private TracePoint[] Tos { get; } = [.. Calls.Select(c => c.To)];

    public double GetStart(TraceTimelineAxis axis) => Scale.GetValue(Start, axis);

    public double GetEnd(TraceTimelineAxis axis) => Scale.GetValue(End, axis);

    public double GetConstructed(TraceTimelineAxis axis) => Scale.GetValue(Constructed, axis);

    public double GetDestroyed(TraceTimelineAxis axis) => Scale.GetValue(Destroyed, axis);

    public int GetFirstEndingAfter(TraceTimelineAxis axis, double value, int from = 0) => Scale.GetFirstAfter(Tos, axis, value, from);
}
