using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceRead(TraceReadType Type,
                               PageAddress Page,
                               int PageCount,
                               uint Thread,
                               CallStackNode Node,
                               int Call,
                               TraceSpan Position,
                               TraceSpan Instructions) : ITraceMark
{
    public ExecutionOperatorEvent? Operator { get; init; }

    public TraceSpan Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Position : Instructions;
}
