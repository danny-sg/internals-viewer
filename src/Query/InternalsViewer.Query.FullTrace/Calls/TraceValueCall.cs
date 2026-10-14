using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceValueCall(int Call, ulong Sequence, CallStackNode? Node, uint Thread = 0);
