namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public readonly record struct TracePageRead(TracePosition Position, uint Thread, ulong Address, ushort Length);
