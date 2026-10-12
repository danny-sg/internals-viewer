using InternalsViewer.Query.CallStack.TimeTravel.Timeline.Enums;

namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline;

public readonly record struct TimeTravelTimelineSpan(double Start, double End, int Node, int Call, TimeTravelSpanFlags Flags);
