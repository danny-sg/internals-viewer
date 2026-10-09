namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public sealed record TimeTravelMemoryUse(string Caller, string Allocator, ulong Bytes, long Allocations);
