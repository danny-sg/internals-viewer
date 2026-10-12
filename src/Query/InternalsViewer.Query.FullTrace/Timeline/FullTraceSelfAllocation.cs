namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct FullTraceSelfAllocation(int Thread, int Depth, int Index, ulong Bytes);
