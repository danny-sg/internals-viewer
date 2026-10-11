namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public readonly record struct TimeTravelFree(uint Thread, double Start, double End, ulong Pointer);
