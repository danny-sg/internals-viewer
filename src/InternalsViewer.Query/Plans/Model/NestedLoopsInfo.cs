using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Query.Plans.Model;

public sealed record NestedLoopsInfo
{
    /// <summary>
    /// Predicate the join applies to each pair the inner side returned, or null when there is none
    /// </summary>
    /// <remarks>
    /// A loop join with a correlated seek usually has none, because the seek itself has already restricted the inner side to the matching
    /// rows. One appears when part of the join condition could not be pushed into that seek.
    /// </remarks>
    public AccessPredicate? Predicate { get; init; }

    public bool HasUntranslatedPredicate { get; init; }
}
