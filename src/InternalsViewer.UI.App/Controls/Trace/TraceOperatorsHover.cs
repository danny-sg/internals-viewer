using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Controls.Trace;

internal readonly record struct TraceOperatorsHover(TraceOperatorHit? Operator,
                                                    TraceOperatorHit? ReturnStrip,
                                                    TraceCallLinkHit? CallLink,
                                                    TraceOutputRow? OutputRow,
                                                    TraceRead? Read,
                                                    TraceOperatorHit? ReadStrip,
                                                    TraceEvaluation? Evaluation,
                                                    TraceOperatorHit? EvaluationStrip)
{
    public static TraceOperatorsHover None { get; } = new(null, null, null, null, null, null, null, null);
}
