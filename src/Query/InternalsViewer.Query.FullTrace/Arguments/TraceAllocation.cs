namespace InternalsViewer.Query.FullTrace.Arguments;

public sealed record TraceAllocation(ulong Address, string Allocator, double First, double Last);
