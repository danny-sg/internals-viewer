using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceTick(byte Value,
                               uint Thread,
                               CallStackNode Node,
                               int Call,
                               TraceSpan Position,
                               TraceSpan Instructions) : ITraceMark
{
    public ExecutionOperatorEvent? Operator { get; init; }

    public ExecutionOperatorEvent? Caller { get; init; }

    public ulong Function { get; init; }

    public ulong Instance { get; init; }

    public int Index { get; init; }

    public TraceSpan Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Position : Instructions;
}
