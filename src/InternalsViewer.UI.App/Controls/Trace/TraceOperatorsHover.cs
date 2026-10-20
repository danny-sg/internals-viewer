using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Controls.Trace;

internal readonly record struct TraceOperatorsHover(TraceOperatorHit? Operator,
                                                    TraceCallLinkHit? CallLink,
                                                    TraceOutputRow? OutputRow,
                                                    TraceTickHit? Tick)
{
    public static TraceOperatorsHover None { get; } = new(null, null, null, null);
}
