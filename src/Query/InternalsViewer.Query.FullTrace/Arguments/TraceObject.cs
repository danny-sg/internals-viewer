namespace InternalsViewer.Query.FullTrace.Arguments;

public sealed record TraceObject(ulong Address, string ClassName, double First, double Last);
