using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record FullTraceEventLink(EngineEvent Event, CallStackNode? Node, uint ThreadId, double Position, double Instructions)
{
    public double StartOf(FullTraceTimelineAxis axis) => axis == FullTraceTimelineAxis.Position ? Position : Instructions;
}
