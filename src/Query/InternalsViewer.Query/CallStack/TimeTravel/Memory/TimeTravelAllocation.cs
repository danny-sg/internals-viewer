namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public readonly record struct TimeTravelAllocation(uint Thread, double Start, double End, ulong Bytes, ulong Pointer);
