using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;

namespace InternalsViewer.Query.Parsing.Plans;

/// <summary>
/// The seek ranges and residual predicate a data access operator applies
/// </summary>
/// <remarks>
/// A seek narrows the rows read using the index key, then a residual predicate filters what the seek
/// returned. Keeping them apart preserves the distinction the plan makes, which is what separates
/// rows read from rows output.
/// </remarks>
public sealed class PredicateInfo
{
    /// <summary>
    /// Key ranges the seek is restricted to, empty for a scan
    /// </summary>
    /// <remarks>
    /// More than one range appears when a single operator seeks several times, which is how an IN
    /// list is executed.
    /// </remarks>
    public ImmutableArray<SeekBounds> SeekBounds { get; init; } = [];

    /// <summary>
    /// Predicate applied to rows the access returned, or null when there is none
    /// </summary>
    public AccessPredicate? Residual { get; init; }

    /// <summary>
    /// Whether the operator stated a predicate that could not be translated
    /// </summary>
    /// <remarks>
    /// Distinguishes an operator with no predicate from one whose predicate was not representable,
    /// so a caller does not mistake an untranslated filter for an absent one.
    /// </remarks>
    public bool HasUntranslatedPredicate { get; init; }

    public long? RowGoal { get; set; }

    public bool HasSeekBounds => !SeekBounds.IsDefaultOrEmpty;
}
