using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

internal readonly record struct TraceTickSource(TraceTimelineThread Thread, int SpanIndex, byte Value, TracePoint From, TracePoint To)
{
    public ExecutionOperatorEvent? Operator { get; init; }

    public ExecutionOperatorEvent? Caller { get; init; }

    public static TraceTickSource Create(TraceSpanLocation location, byte value)
        => new(location.Thread, location.Row.GetStoreIndex(location.Index), value, location.From, location.To);

    public static TraceTickSource Create(TraceSpanLocation location, byte value, TracePoint point)
        => new(location.Thread, location.Row.GetStoreIndex(location.Index), value, point, point);
}
