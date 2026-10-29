using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public readonly record struct TraceBufferPage(ulong Frame, PageAddress Page, ushort DatabaseId, TracePosition Position, ulong Buffer = 0);
