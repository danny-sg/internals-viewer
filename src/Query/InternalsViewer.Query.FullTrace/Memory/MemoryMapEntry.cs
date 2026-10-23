using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed record MemoryMapEntry(ulong Address,
                                    MemoryMapOperation Operation,
                                    int CallIndex,
                                    ulong Owner,
                                    TracePosition FromPosition,
                                    TracePosition ToPosition,
                                    uint Thread,
                                    int CallNodeIndex,
                                    string? ClassName,
                                    string? Allocator,
                                    ulong Bytes = 0,
                                    string? Site = null);
