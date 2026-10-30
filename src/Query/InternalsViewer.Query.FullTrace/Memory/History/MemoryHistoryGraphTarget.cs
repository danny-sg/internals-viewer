using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryGraphTarget(TracePosition From,
                                                       TracePosition To,
                                                       ulong Value,
                                                       int Block,
                                                       TraceBufferReference? Page);
