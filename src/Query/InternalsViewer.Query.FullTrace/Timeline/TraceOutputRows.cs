using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceOutputRows
{
    private TraceOutputRows(TraceOutputRow[] rows, IReadOnlyList<TraceOutputRowSource> sources)
    {
        Rows = rows;

        Sources = sources;

        Positions = AxisRows.Build(rows, TraceTimelineAxis.Position);

        Instructions = AxisRows.Build(rows, TraceTimelineAxis.Instructions);
    }

    public static TraceOutputRows Empty { get; } = new([], []);

    public int Count => Rows.Length;

    public IReadOnlyList<TraceOutputRow> All => Rows;

    public IReadOnlyList<TraceOutputRowSource> Sources { get; }

    private TraceOutputRow[] Rows { get; }

    private AxisRows Positions { get; }

    private AxisRows Instructions { get; }

    public ReadOnlySpan<double> GetMarks(TraceTimelineAxis axis) => Of(axis).Values;

    public TraceOutputRow? GetNearest(TraceTimelineAxis axis, double value)
    {
        var rows = Of(axis);

        if (rows.Values.Length == 0)
        {
            return null;
        }

        var index = SortedSearch.FirstAtOrAfter(rows.Values, value);

        if (index > 0 && (index == rows.Values.Length || value - rows.Values[index - 1] <= rows.Values[index] - value))
        {
            index--;
        }

        return Rows[rows.Order[index]];
    }

    public TraceOutputRow? GetRow(TraceTimelineAxis axis, double value)
    {
        var rows = Of(axis);

        var index = SortedSearch.FirstAtOrAfter(rows.Values, value);

        if (index >= rows.Values.Length)
        {
            return null;
        }

        var row = Rows[rows.Order[index]];

        return row.Return.GetStart(axis) <= value ? row : null;
    }

    internal static TraceOutputRows Build(IEnumerable<ExecutionOperatorEvent> operators,
                                          IReadOnlyList<TraceRowReturn> passedUp,
                                          IReadOnlyList<TraceOutputRowSource> sources)
    {
        var rootOperator = operators.Where(o => o.PlanNodeIdentifier is not null).MinBy(o => o.PlanNodeIdentifier!.NodeId);

        bool IsRoot(TraceRowReturn rowReturn) => ReferenceEquals(rowReturn.Operator, rootOperator);

        TraceRowReturn[] roots = [.. passedUp.Where(IsRoot).OrderBy(r => r.FromPosition)];

        if (roots.Length == 0)
        {
            return Empty;
        }

        var returns = passedUp.Where(r => !IsRoot(r))
                              .GroupBy(r => r.Thread)
                              .ToDictionary(g => g.Key, g => g.OrderBy(r => r.FromPosition).ToArray());

        var rows = new TraceOutputRow[roots.Length];

        for (var index = 0; index < roots.Length; index++)
        {
            var root = roots[index];

            var steps = returns.TryGetValue(root.Thread, out var thread) ? GetSteps(thread, root) : [];

            rows[index] = new TraceOutputRow(index + 1, root, steps);
        }

        return new TraceOutputRows(rows, sources);
    }

    private static TraceOutputStep[] GetSteps(TraceRowReturn[] returns, TraceRowReturn root)
    {
        var path = new HashSet<TraceRowReturn>(ReferenceEqualityComparer.Instance) { root };

        var steps = new List<TraceOutputStep>();

        var index = SortedSearch.FirstAtOrAfter(returns, root.FromPosition, r => r.FromPosition);

        for (; index < returns.Length && returns[index].FromPosition < root.ToPosition; index++)
        {
            var rowReturn = returns[index];

            if (rowReturn.ToPosition <= root.ToPosition && rowReturn.Consumer is { } consumer && path.Contains(consumer))
            {
                path.Add(rowReturn);

                steps.Add(new TraceOutputStep(rowReturn, consumer));
            }
        }

        return [.. steps];
    }

    private AxisRows Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Positions : Instructions;

    private sealed record AxisRows(double[] Values, int[] Order)
    {
        public static AxisRows Build(TraceOutputRow[] rows, TraceTimelineAxis axis)
        {
            int[] order = [.. Enumerable.Range(0, rows.Length).OrderBy(i => rows[i].Return.GetValue(axis))];

            return new AxisRows([.. order.Select(i => rows[i].Return.GetValue(axis))], order);
        }
    }
}
