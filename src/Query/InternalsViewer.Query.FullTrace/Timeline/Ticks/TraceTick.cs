using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed record TraceTick(byte Value, uint Thread, CallStackNode Node, TraceTimelineRow Row, int RowIndex) : ITraceMark
{
    public ExecutionOperatorEvent? Operator { get; init; }

    public ExecutionOperatorEvent? Caller { get; init; }

    public TraceSpan Span => Row.GetSpan(RowIndex);

    public int CallIndex => Row.GetCallIndex(RowIndex);

    public ulong FunctionAddress => Node.Frame?.Address ?? 0;

    public ulong InstanceAddress => Node.Frame?.InstanceAddress ?? 0;
}
