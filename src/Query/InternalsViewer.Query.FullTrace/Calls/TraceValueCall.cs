using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceValueCall(int CallIndex, TracePosition Position, CallTreeNode? Node, uint Thread = 0);
