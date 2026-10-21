using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed record TraceTick(byte Value, uint Thread, CallStackNode Node, int Call, TraceSpan Span) : ITraceMark
{
    public ExecutionOperatorEvent? Operator { get; init; }

    public ExecutionOperatorEvent? Caller { get; init; }

    public ulong Function { get; init; }

    public ulong Instance { get; init; }

    public int Index { get; init; }
}
