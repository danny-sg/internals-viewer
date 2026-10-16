using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceValueCall(int Call, TracePosition Position, CallStackNode? Node, uint Thread = 0);
