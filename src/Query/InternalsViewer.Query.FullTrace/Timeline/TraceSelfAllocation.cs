namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceSelfAllocation(int Thread, int Depth, int Index, ulong Bytes);
