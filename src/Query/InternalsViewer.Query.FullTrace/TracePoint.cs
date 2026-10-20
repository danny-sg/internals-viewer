namespace InternalsViewer.Query.FullTrace;

internal readonly record struct TracePoint(TracePosition Position, ulong Instructions);
