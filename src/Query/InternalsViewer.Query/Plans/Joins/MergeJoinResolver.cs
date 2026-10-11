using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// Identifies merge joins that can be traced, both inputs supplying the stated join keys
/// </summary>
/// <remarks>
/// A side may itself be an operator, because the trace builds that subtree too. Whether the operator can actually be simulated is left to
/// whoever builds it, which refuses anything it has no case for and takes the whole trace with it.
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

        if (node.MergeInfo is not { } mergeInfo
            || mergeInfo.OuterKeys.Count == 0
            || mergeInfo.OuterKeys.Count != mergeInfo.InnerKeys.Count)
        {
            return null;
        }

        if (!JoinSideColumns.KeysMatchSide(mergeInfo.OuterKeys, outer)
            || !JoinSideColumns.KeysMatchSide(mergeInfo.InnerKeys, inner))
        {
            return null;
        }

        return new MergeJoin(node, outer, inner, JoinTypeParser.Parse(node.LogicalOperator));
    }
}
