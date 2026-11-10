namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public readonly record struct TraceHashBucket(int Index, ulong Address, int Count, ulong Key, ulong Row);
