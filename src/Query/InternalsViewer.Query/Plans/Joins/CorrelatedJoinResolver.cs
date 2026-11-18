using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// Identifies nested loops joins whose inner side is driven by values from the outer row, such as a key or RID lookup
/// </summary>
/// <remarks>
/// A seek is only resolved when every correlated column can be traced to a column the outer side outputs, since those are the values the
/// trace binds on each rebind.
///
/// A lookup needs no such check as it takes the bookmark of the row it is fetching from the outer row by definition, and for a heap that
/// bookmark is a row identifier rather than a column the outer visibly carries.
/// </remarks>
public static class CorrelatedJoinResolver
{
    /// <summary>
    /// Whether the inner side fetches from a heap by row identifier rather than seeking an index by key
    /// </summary>
    public static bool IsRidLookup(PlanNode inner)
        => OperatorClassifier.IsLookup(inner)
           && inner.PhysicalOperator.Contains("RID", StringComparison.OrdinalIgnoreCase);
}
