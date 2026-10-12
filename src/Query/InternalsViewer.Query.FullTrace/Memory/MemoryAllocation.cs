namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryAllocation(uint Thread,
                                                 double Start,
                                                 double End,
                                                 ulong Bytes,
                                                 ulong Pointer,
                                                 bool Workspace = false);
