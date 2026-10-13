using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceTimelineSpan(double Start, double End, int Node, int Call, TraceSpanFlags Flags);
