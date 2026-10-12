namespace InternalsViewer.Query.Memory;

public sealed record FullTraceMemoryUse(string Caller, string Allocator, ulong Bytes, long Allocations);
