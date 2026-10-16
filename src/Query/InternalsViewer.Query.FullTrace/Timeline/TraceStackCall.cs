namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceStackCall(TraceSpan Span, double Instructions);
