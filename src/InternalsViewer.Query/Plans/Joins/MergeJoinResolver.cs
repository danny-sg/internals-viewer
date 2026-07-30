using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// Identifies merge joins whose sides can be traced, both inputs reading an index directly with the join keys stated
/// </summary>
/// <remarks>
/// A merge join fed by a sort or another join is not resolved because the traced walks would not reproduce the ordered stream the join
/// consumed.
/// </remarks>
public static class MergeJoinResolver
{
    public static MergeJoin? Resolve(PlanNode node)
    {
        if (!OperatorClassifier.IsMergeJoin(node) || node.Children.Count < 2)
        {
            return null;
        }

        var outer = node.Children[0];

        var inner = node.Children[1];

        if (!OperatorClassifier.IsRead(outer) || !OperatorClassifier.IsRead(inner))
        {
            return null;
        }

        if (node.MergeInfo is not { } mergeInfo
            || mergeInfo.OuterKeys.Count == 0
            || mergeInfo.OuterKeys.Count != mergeInfo.InnerKeys.Count)
        {
            return null;
        }

        if (!KeysMatchTable(mergeInfo.OuterKeys, outer) || !KeysMatchTable(mergeInfo.InnerKeys, inner))
        {
            return null;
        }

        return new MergeJoin(node, outer, inner);
    }

    private static bool KeysMatchTable(List<ColumnReference> keys, PlanNode side)
    {
        return keys.All(k => k.Table.Length == 0
                             || string.Equals(Trim(k.Table), Trim(side.Table ?? string.Empty), StringComparison.OrdinalIgnoreCase));
    }

    private static string Trim(string name)
    {
        return name.Trim('[', ']');
    }
}
