using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceEventLink(EngineEvent Event, CallStackNode? Node, uint ThreadId, double Position, double Instructions)
{
    public double StartOf(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Position : Instructions;
}
