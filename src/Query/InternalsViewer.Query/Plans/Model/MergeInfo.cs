using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Query.Plans.Model;

public sealed record MergeInfo
{
    public List<ColumnReference> OuterKeys { get; init; } = [];

    public List<ColumnReference> InnerKeys { get; init; } = [];

    public bool ManyToMany { get; init; }

    /// <summary>
    /// Predicate applied to a pair whose join columns already compared equal, or null when there is none
    /// </summary>
    public AccessPredicate? Residual { get; init; }

    public bool HasUntranslatedResidual { get; init; }
}
