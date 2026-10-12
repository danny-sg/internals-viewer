namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryFree(uint Thread, double Start, double End, ulong Pointer);
