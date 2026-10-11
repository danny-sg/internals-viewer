namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline;

public readonly record struct TimeTravelSelfAllocation(int Thread, int Depth, int Index, ulong Bytes);
