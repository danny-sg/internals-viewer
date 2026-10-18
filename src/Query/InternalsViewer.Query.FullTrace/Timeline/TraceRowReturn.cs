using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceRowReturn(ExecutionOperatorEvent Operator,
                                    uint Thread,
                                    CallStackNode Node,
                                    int Call,
                                    RowReturnType Type,
                                    double Position,
                                    double Instructions)
{
    public double StartPosition { get; init; }

    public double StartInstructions { get; init; }

    internal TracePosition FromPosition { get; init; }

    internal TracePosition ToPosition { get; init; }

    internal TraceRowReturn? Consumer { get; set; }

    public double GetValue(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Position : Instructions;

    public double GetStart(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? StartPosition : StartInstructions;
}
