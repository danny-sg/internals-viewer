using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Operators;

internal sealed record TraceOperatorRow(ExecutionOperatorEvent Operator,
                                        TraceOperatorLifetime[] Lifetimes,
                                        string Name,
                                        string Target,
                                        string Title)
{
    public ExecutionOperatorEvent TickOperator { get; init; } = Operator;

    public bool IsStatement { get; init; }
}
