namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryMapAllocation(ulong Bytes, string? Site, string? MemoryObject);
