namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryAllocation(uint Thread,
                                                 TracePosition Start,
                                                 TracePosition End,
                                                 ulong Bytes,
                                                 ulong Pointer,
                                                 bool Workspace = false);
