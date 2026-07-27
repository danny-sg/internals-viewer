using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Text;

/// <summary>
/// A formatted predicate ready to be displayed
/// </summary>
/// <remarks>
/// Holding the tokens in a single object keeps the decision of what to write with the caller that has the model, leaving a renderer to
/// deal only with how the tokens should look.
/// </remarks>
public sealed record PredicateText(ImmutableArray<PredicateToken> Tokens)
{
    /// <summary>
    /// Nothing to display
    /// </summary>
    public static readonly PredicateText Empty = new([]);

    public bool IsEmpty => Tokens.IsDefaultOrEmpty;

    /// <summary>
    /// Formats a predicate
    /// </summary>
    public static PredicateText From(AccessPredicate predicate)
    {
        return new PredicateText(PredicateWriter.Write(predicate));
    }

    /// <summary>
    /// Formats a seek range, labelling it with the index key columns when they are known
    /// </summary>
    public static PredicateText From(SeekBounds bounds, ImmutableArray<string> keyColumns = default)
    {
        return new PredicateText(PredicateWriter.Write(bounds, keyColumns));
    }

    /// <summary>
    /// Formats a seek range, labelling it with the index key columns when they are known
    /// </summary>
    public static PredicateText From(SeekBounds bounds, IEnumerable<string>? keyColumns)
    {
        return From(bounds, keyColumns is null ? default : [.. keyColumns]);
    }

    public override string ToString()
    {
        return PredicateWriter.ToText(Tokens);
    }
}
