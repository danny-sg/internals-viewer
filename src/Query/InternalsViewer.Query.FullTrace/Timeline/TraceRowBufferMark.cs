using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceRowBufferMark(ExecutionOperatorEvent Operator,
                                        CallStackNode Node,
                                        int Call,
                                        double Position,
                                        double Instructions)
{
    public double GetValue(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Position : Instructions;
}
