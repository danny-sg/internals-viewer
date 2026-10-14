using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceSpan(double Start, double End, int Node, int Call, TraceSpanFlags Flags);
