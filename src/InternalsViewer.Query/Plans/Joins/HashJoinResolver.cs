using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// Identifies hash matches that can be traced, both inputs supplying the stated hash keys
/// </summary>
/// <remarks>
/// The build side is the first input, which is the one a hash match reads to completion before the second is opened. A side may itself be
/// an operator, because the trace builds that subtree too and so reproduces the stream the join consumed.
/// </remarks>
public static class HashJoinResolver
{
    public static HashJoin? Resolve(PlanNode node)
    {
        if (!OperatorClassifier.IsHashJoin(node) || OperatorClassifier.IsHashAggregate(node) || node.Children.Count < 2)
        {
            return null;
        }

        if (OperatorClassifier.GetHashBuildChild(node) is not { } build
            || OperatorClassifier.GetHashProbeChild(node) is not { } probe)
        {
            return null;
        }

        if (node.HashInfo is not { } hashInfo
            || hashInfo.BuildKeys.Count == 0
            || hashInfo.BuildKeys.Count != hashInfo.ProbeKeys.Count)
        {
            return null;
        }

        if (!JoinSideColumns.KeysMatchSide(hashInfo.BuildKeys, build)
            || !JoinSideColumns.KeysMatchSide(hashInfo.ProbeKeys, probe))
        {
            return null;
        }

        return new HashJoin(node, build, probe, JoinTypeParser.Parse(node.LogicalOperator));
    }
}
