namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

internal readonly record struct MemoryAllocation(uint Thread,
                                                 TracePosition Start,
                                                 TracePosition End,
                                                 ulong Bytes,
                                                 ulong Pointer,
                                                 bool IsWorkspace = false);
