using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceValueCall(int CallIndex, TracePosition Position, CallStackNode? Node, uint Thread = 0);
