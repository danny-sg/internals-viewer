using InternalsViewer.Execution.AccessPaths.Joins;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// Reads the logical join a plan operator states it is carrying out
/// </summary>
public static class JoinTypeParser
{
    public static JoinType Parse(string? logicalOperator)
    {
        if (string.IsNullOrEmpty(logicalOperator))
        {
            return JoinType.Inner;
        }

        var isLeft = Contains(logicalOperator, "Left");

        if (Contains(logicalOperator, "Anti Semi"))
        {
            return isLeft ? JoinType.LeftAntiSemi : JoinType.RightAntiSemi;
        }

        if (Contains(logicalOperator, "Semi"))
        {
            return isLeft ? JoinType.LeftSemi : JoinType.RightSemi;
        }

        if (Contains(logicalOperator, "Full Outer"))
        {
            return JoinType.FullOuter;
        }

        if (Contains(logicalOperator, "Outer"))
        {
            return isLeft ? JoinType.LeftOuter : JoinType.RightOuter;
        }

        return JoinType.Inner;
    }

    private static bool Contains(string value, string term)
        => value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
