namespace InternalsViewer.Execution.AccessPaths.Joins;

public static class JoinTypeExtensions
{
    /// <summary>
    /// Whether an outer row with no match still reaches the output
    /// </summary>
    /// <remarks>
    /// Left/Full/Left Anti-Semi joins will output Outer rows without a join match
    /// </remarks>
    public static bool PreservesOuter(this JoinType type)
        => type is JoinType.LeftOuter or JoinType.FullOuter or JoinType.LeftAntiSemi;

    /// <summary>
    /// Whether an inner row with no match still reaches the output
    /// </summary>
    /// <remarks>
    /// Right/Full/Right Anti-Semi joins will output Inner rows without a join match
    /// </remarks>
    public static bool PreservesInner(this JoinType type)
        => type is JoinType.RightOuter or JoinType.FullOuter or JoinType.RightAntiSemi;

    /// <summary>
    /// Whether a match produces one row per pair, rather than a single row from the side being tested
    /// </summary>
    public static bool EmitsPairs(this JoinType type)
        => type is JoinType.Inner or JoinType.LeftOuter or JoinType.RightOuter or JoinType.FullOuter;

    /// <summary>
    /// Whether a match emits the outer row alone, as a test for existence
    /// </summary>
    public static bool EmitsOuterOnMatch(this JoinType type)
        => type is JoinType.LeftSemi;

    /// <summary>
    /// Whether a match emits the inner row alone, as a test for existence
    /// </summary>
    public static bool EmitsInnerOnMatch(this JoinType type)
        => type is JoinType.RightSemi;

    /// <summary>
    /// What the join requires of each side for a row to reach the output
    /// </summary>
    public static (JoinSlotRule Outer, JoinSlotRule Inner) EmitRule(this JoinType type)
        => type switch
        {
            JoinType.LeftOuter => (JoinSlotRule.Present, JoinSlotRule.Any),
            JoinType.RightOuter => (JoinSlotRule.Any, JoinSlotRule.Present),
            JoinType.FullOuter => (JoinSlotRule.Any, JoinSlotRule.Any),
            JoinType.LeftAntiSemi => (JoinSlotRule.Present, JoinSlotRule.Absent),
            JoinType.RightAntiSemi => (JoinSlotRule.Absent, JoinSlotRule.Present),
            _ => (JoinSlotRule.Present, JoinSlotRule.Present)
        };

    /// <summary>
    /// Builds the decision a join makes when it has weighed one side against the other
    /// </summary>
    public static JoinDecision Decide(this JoinType type, bool hasOuter, bool hasInner)
    {
        var (outerRule, innerRule) = type.EmitRule();

        return new JoinDecision(hasOuter, hasInner, outerRule, innerRule, type.ToDisplayName());
    }

    public static string ToDisplayName(this JoinType type)
        => type switch
        {
            JoinType.LeftOuter => "Left Outer Join",
            JoinType.RightOuter => "Right Outer Join",
            JoinType.FullOuter => "Full Outer Join",
            JoinType.LeftSemi => "Left Semi Join",
            JoinType.LeftAntiSemi => "Left Anti Semi Join",
            JoinType.RightSemi => "Right Semi Join",
            JoinType.RightAntiSemi => "Right Anti Semi Join",
            _ => "Inner Join"
        };
}