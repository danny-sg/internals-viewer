using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public readonly record struct TracePageAccess(TracePosition Position,
                                              PageAddress Page,
                                              ushort DatabaseId,
                                              int? Offset,
                                              int CallNodeIndex,
                                              int CallIndex);
