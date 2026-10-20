namespace InternalsViewer.UI.App.Controls.Trace;

internal readonly record struct TraceOperatorsHover(TraceOperatorHit? Operator, TraceTickHit? Tick)
{
    public static TraceOperatorsHover None { get; } = new(null, null);
}
