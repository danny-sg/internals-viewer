namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryFree(uint Thread, TracePosition Start, TracePosition End, ulong Pointer);
