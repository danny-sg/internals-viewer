namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

internal readonly record struct TimeTravelFree(uint Thread, double Start, double End, ulong Pointer);
