using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.FullTrace.CallLog;

public sealed record FullTraceValueMatch(int Call, ulong Sequence, CallStackNode? Node);
