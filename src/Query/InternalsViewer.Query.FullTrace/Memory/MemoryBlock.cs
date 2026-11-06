namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryBlock(ulong Pointer, ulong Bytes, uint Thread, TracePosition Start, TracePosition End);
