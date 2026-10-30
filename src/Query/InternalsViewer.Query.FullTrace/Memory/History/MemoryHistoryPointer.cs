using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryPointer(int Offset, ulong Value, int Block, TraceBufferReference? Page);
