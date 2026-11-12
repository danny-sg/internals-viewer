using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Operators;

internal readonly record struct TraceOperatorsHover(TraceOperatorHit? Operator, TraceTickHit? Tick, TraceStage? Stage)
{
    public static TraceOperatorsHover None { get; } = new(null, null, null);
}
