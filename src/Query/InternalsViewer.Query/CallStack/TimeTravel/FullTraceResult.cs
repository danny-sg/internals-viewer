namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record FullTraceResult(CallStackTree CallStack, TimeTravelCallLog? CallLog);
