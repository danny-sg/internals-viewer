namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceThreadStack(uint ThreadId, IReadOnlyList<TraceStackCall> Calls);
