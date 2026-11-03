namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

internal readonly record struct MemoryFree(uint Thread, TracePosition Start, TracePosition End, ulong Pointer);
