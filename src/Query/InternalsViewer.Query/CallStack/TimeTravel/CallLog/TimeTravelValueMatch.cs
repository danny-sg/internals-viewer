namespace InternalsViewer.Query.CallStack.TimeTravel.CallLog;

public sealed record TimeTravelValueMatch(int Call, ulong Sequence, CallStackNode? Node);
