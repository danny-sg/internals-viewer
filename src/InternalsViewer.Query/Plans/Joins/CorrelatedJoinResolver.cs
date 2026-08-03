using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// Identifies nested loops joins whose inner side is driven by values from the outer row, such as a key or RID lookup
/// </summary>
/// <remarks>
/// A seek is only resolved when every correlated column can be traced to a column the outer side outputs, since those are the values the
/// trace binds on each rebind.
///
/// A lookup needs no such check as it takes the bookmark of the row it is fetching from the outer row by definition, and for a heap that
/// bookmark is a row identifier rather than a column the outer visibly carries.
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

        // RID Lookup - no check needed as it is a physical location
        if (IsRidLookup(inner))
        {
            return new CorrelatedJoin(node, outer, inner, JoinTypeParser.Parse(node.LogicalOperator));
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

    /// <summary>
    /// Whether the inner side fetches from a heap by row identifier rather than seeking an index by key
    /// </summary>
    public static bool IsRidLookup(PlanNode inner)
        => OperatorClassifier.IsLookup(inner)
           && inner.PhysicalOperator.Contains("RID", StringComparison.OrdinalIgnoreCase);

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
