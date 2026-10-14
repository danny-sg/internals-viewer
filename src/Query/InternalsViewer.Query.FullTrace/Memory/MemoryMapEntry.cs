namespace InternalsViewer.Query.FullTrace.Memory;

public sealed record MemoryMapEntry(ulong Address,
                                    MemoryMapOperation Operation,
                                    ulong Call,
                                    ulong Owner,
                                    ulong Until,
                                    uint Thread,
                                    int Node,
                                    string? ClassName,
                                    string? Allocator);
