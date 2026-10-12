using InternalsViewer.Query.CallStack.TimeTravel.Timeline.Enums;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline;

public sealed record TimeTravelEventLink(EngineEvent Event, CallStackNode? Node, uint ThreadId, double Position, double Instructions)
{
    public double StartOf(TimeTravelTimelineAxis axis) => axis == TimeTravelTimelineAxis.Position ? Position : Instructions;
}
