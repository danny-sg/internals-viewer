using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public readonly record struct TraceTick(byte Value,
                                        uint Thread,
                                        CallTreeNode Node,
                                        TraceSpan Span,
                                        ExecutionOperatorEvent Operator,
                                        ExecutionOperatorEvent? Caller)
{
    public int CallIndex => Span.CallIndex;

    public ulong FunctionAddress => Node.Frame?.Address ?? 0;

    public ulong InstanceAddress => Node.Frame?.InstanceAddress ?? 0;
}
