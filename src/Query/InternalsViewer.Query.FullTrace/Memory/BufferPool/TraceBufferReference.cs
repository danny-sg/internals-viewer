using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public readonly record struct TraceBufferReference(PageAddress Page, ushort DatabaseId, int Offset, bool IsBuffer = false);
