using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Evaluations;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceEvaluations
{
    private TraceEvaluations(TraceEvaluation[] evaluations, IReadOnlyList<EvaluationCapture> captures)
    {
        Evaluations = evaluations;

        Captures = captures;

        ByOperator = OperatorMarks<TraceEvaluation>.Build(evaluations, e => e.Operator, e => e.Thread);
    }

    public static TraceEvaluations Empty { get; } = new([], []);

    public int Count => Evaluations.Length;

    public IReadOnlyList<TraceEvaluation> All => Evaluations;

    private TraceEvaluation[] Evaluations { get; }

    private IReadOnlyList<EvaluationCapture> Captures { get; }

    private OperatorMarks<TraceEvaluation> ByOperator { get; }

    public TraceMarkIndex<TraceEvaluation>? GetEvaluations(ExecutionOperatorEvent operatorEvent, uint thread)
        => ByOperator.Get(operatorEvent, thread);

    internal static TraceEvaluations Build(TraceTimeline timeline, TraceCallStore calls, IReadOnlyList<EvaluationCapture> captures)
    {
        var evaluations = new List<TraceEvaluation>(captures.Count);

        var froms = new List<TracePosition>(captures.Count);

        var lists = new Dictionary<ulong, TraceCallList?>();

        foreach (var capture in captures)
        {
            if (!lists.TryGetValue(capture.Function, out var list))
            {
                list = calls.GetCallList(capture.Function, 0);

                lists[capture.Function] = list;
            }

            if (list is null
                || capture.Call < 0
                || capture.Call >= list.Count
                || timeline.FindCall(capture.Function, 0, capture.Call) is not { } location)
            {
                continue;
            }

            evaluations.Add(new TraceEvaluation(capture.Result,
                                                list.GetThread(capture.Call),
                                                location.Node,
                                                location.Call,
                                                location.Span(TraceTimelineAxis.Position),
                                                location.Span(TraceTimelineAxis.Instructions)));

            froms.Add(list.GetFromPosition(capture.Call));
        }

        var operators = OperatorMarks<TraceEvaluation>.GetEnclosing(calls,
                                                                    timeline.GetOperatorsByInstance(),
                                                                    [.. evaluations.Select((e, i) => (e.Thread, froms[i]))]);

        TraceEvaluation[] located = [.. evaluations.Select((e, i) => operators[i] is { } found ? e with { Operator = found } : e)];

        return new TraceEvaluations(located, captures);
    }
}
