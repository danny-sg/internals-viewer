namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record TimeTravelValueMatch(int Call, ulong Sequence, CallStackNode? Node);
