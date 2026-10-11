using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Query.Plans.Model;

public sealed class HashInfo
{
    public List<ColumnReference> BuildKeys { get; set; } = [];

    public List<ColumnReference> ProbeKeys { get; set; } = [];

    /// <summary>
    /// Predicate applied to a pair that already matched on the hash key, or null when there is none
    /// </summary>
    /// <remarks>
    /// A hash join can only hash equality, so any other part of the join condition is tested on each candidate pair instead. A join on a
    /// nullable column also picks one up, because the residual is what enforces that a NULL never equals a NULL.
    /// </remarks>
    public AccessPredicate? Residual { get; set; }

    public bool HasUntranslatedResidual { get; set; }
}
