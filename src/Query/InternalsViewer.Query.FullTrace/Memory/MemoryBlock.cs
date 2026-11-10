namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryBlock(ulong Address, ulong Size, uint Thread, TracePosition FromPosition, TracePosition ToPosition);
