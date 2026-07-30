using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Query.Parsing.Plans;

/// <summary>
/// The seek ranges and residual predicate a data access operator applies
/// </summary>
/// <remarks>
/// Seek Bounds - bounds for the seek
/// Residual    - A further predicate to be applied within the bounds
///
/// The two types are separate because the seek may not be fully translatable to a range. The seek gets you to a range in the index that
/// can then be further filtered.
///
/// Seek bounds will seek on the range and then the residual predicate will be applied on each row in the range.
/// </remarks>
public sealed class PredicateInfo
{
    /// <summary>
    /// Key ranges the seek is restricted to, empty for a scan
    /// </summary>
    /// <remarks>
    /// More than one range can appear in an operator, for example when using the IN clause. A seek will be made for each which can be seen
    /// in the Scan Count statistic.
    /// </remarks>
    public ImmutableArray<SeekBounds> SeekBounds { get; init; } = [];

    /// <summary>
    /// Predicate applied to rows the access returned, or null when there is none
    /// </summary>
    /// <remarks>
    /// Rows Read vs Rows Output indicates if a residual has been applied
    /// </remarks>
    public AccessPredicate? Residual { get; init; }

    /// <summary>
    /// Whether the operator stated a predicate that could not be translated
    /// </summary>
    /// <remarks>
    /// Where we can't translate the predicate
    /// </remarks>
    public bool HasUntranslatedPredicate { get; init; }

    /// <summary>
    /// Row Goal is used to short circuit the end of the seek
    /// </summary>
    /// <remarks>
    /// The Query Engine will use this if it knows the seek is on a unique key where it will only expect one value, or if TOP has been
    /// used etc.
    /// </remarks>
    public long? RowGoal { get; set; }

    public bool HasSeekBounds => !SeekBounds.IsDefaultOrEmpty;

    public ImmutableArray<CorrelatedSeekColumn> CorrelatedSeekColumns { get; init; } = [];

    public bool IsCorrelatedSeek => !CorrelatedSeekColumns.IsDefaultOrEmpty;
}
