using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// Identifies hash matches whose sides can be traced, both inputs reading a table directly with the hash keys stated
/// </summary>
/// <remarks>
/// The build side is the first input, which is the one a hash match reads to completion before the second is opened. A hash match fed by
/// another join or an aggregate is not resolved because the traced reads would not reproduce the stream the join consumed.
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

        if (!OperatorClassifier.IsRead(build) || !OperatorClassifier.IsRead(probe))
        {
            return null;
        }

        if (node.HashInfo is not { } hashInfo
            || hashInfo.BuildKeys.Count == 0
            || hashInfo.BuildKeys.Count != hashInfo.ProbeKeys.Count)
        {
            return null;
        }

        if (!KeysMatchTable(hashInfo.BuildKeys, build) || !KeysMatchTable(hashInfo.ProbeKeys, probe))
        {
            return null;
        }

        return new HashJoin(node, build, probe, JoinTypeParser.Parse(node.LogicalOperator));
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
