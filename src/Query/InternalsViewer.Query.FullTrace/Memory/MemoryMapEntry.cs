using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed record MemoryMapEntry(ulong Address,
                                    MemoryMapOperation Operation,
                                    int Call,
                                    TracePosition FromPosition,
                                    ulong Owner,
                                    TracePosition ToPosition,
                                    uint Thread,
                                    int Node,
                                    string? ClassName,
                                    string? Allocator);
