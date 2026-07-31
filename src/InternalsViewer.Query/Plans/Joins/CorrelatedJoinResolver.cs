using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// Identifies nested loops joins whose inner side is a correlated seek, such as a key lookup
/// </summary>
/// <remarks>
/// A join is only resolved when every correlated seek column can be traced to a column the outer side outputs, since those are the values
/// the trace binds on each rebind.
/// </remarks>
public static class CorrelatedJoinResolver
{
    public static CorrelatedJoin? Resolve(PlanNode node)
    {
        if (!OperatorClassifier.IsNestedLoop(node) || node.Children.Count < 2)
        {
            return null;
        }

        var outer = node.Children[0];

        var inner = node.Children[1];

        if (!OperatorClassifier.IsRead(inner))
        {
            return null;
        }

        if (inner.PredicateInfo is not { IsCorrelatedSeek: true } predicateInfo)
        {
            return null;
        }

        if (!predicateInfo.CorrelatedSeekColumns.All(c => OutputsColumn(outer, c)))
        {
            return null;
        }

        return new CorrelatedJoin(node, outer, inner, JoinTypeParser.Parse(node.LogicalOperator));
    }

    public static CorrelatedJoin? ResolveFromInner(PlanNode root, PlanNode inner)
    {
        if (FindParent(root, inner) is not { } parent)
        {
            return null;
        }

        var join = Resolve(parent);

        return join?.Inner == inner ? join : null;
    }

    private static bool OutputsColumn(PlanNode outer, CorrelatedSeekColumn column)
    {
        return outer.OutputColumns.Any(o => string.Equals(Trim(o.Table), column.OuterTable, StringComparison.OrdinalIgnoreCase)
                                            && string.Equals(Trim(o.Column), column.OuterColumn, StringComparison.OrdinalIgnoreCase));
    }

    private static PlanNode? FindParent(PlanNode node, PlanNode target)
    {
        foreach (var child in node.Children)
        {
            if (child == target)
            {
                return node;
            }

            if (FindParent(child, target) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string Trim(string name)
    {
        return name.Trim('[', ']');
    }
}
