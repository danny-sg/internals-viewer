using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.FullTrace.CallLog;

public sealed record TraceValueMatch(int Call, ulong Sequence, CallStackNode? Node);
