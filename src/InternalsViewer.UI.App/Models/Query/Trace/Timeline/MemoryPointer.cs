using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public readonly record struct MemoryPointer(int Offset, ulong Value, int Block, TraceBufferReference? Page);
