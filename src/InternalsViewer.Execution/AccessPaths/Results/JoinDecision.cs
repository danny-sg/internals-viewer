namespace InternalsViewer.Execution.AccessPaths.Results;

/// <summary>
/// What a join requires of one side for a row to reach the output
/// </summary>
public enum JoinSlotRule
{
    /// <summary>
    /// A row must have been found on this side
    /// </summary>
    Present,

    /// <summary>
    /// No row must have been found on this side
    /// </summary>
    Absent,

    /// <summary>
    /// The join returns the row either way
    /// </summary>
    Any
}

/// <summary>
/// A join weighing what it has found against what its join type requires
/// </summary>
/// <remarks>
/// Holds both halves of the decision so a reader can see the rule being applied rather than only its outcome.
/// </remarks>
public readonly record struct JoinDecision(bool HasOuter,
                                           bool HasInner,
                                           JoinSlotRule OuterRule,
                                           JoinSlotRule InnerRule,
                                           string JoinName)
{
    public bool IsEmitted => Satisfies(OuterRule, HasOuter) && Satisfies(InnerRule, HasInner);

    private static bool Satisfies(JoinSlotRule rule, bool hasRow)
        => rule switch
        {
            JoinSlotRule.Present => hasRow,
            JoinSlotRule.Absent => !hasRow,
            _ => true
        };
}
