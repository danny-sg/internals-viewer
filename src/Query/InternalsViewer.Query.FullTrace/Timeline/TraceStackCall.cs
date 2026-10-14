namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceStackCall(TraceTimelineSpan Span, double PositionStart, double PositionEnd, double Instructions);
