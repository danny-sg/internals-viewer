using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Text;

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
    /// Formats a seek range, using the column name each boundary value carries
    /// </summary>
    public static PredicateText From(SeekBounds bounds)
    {
        return new PredicateText(PredicateWriter.Write(bounds));
    }

    public static PredicateText From(AccessStep.Probe probe)
    {
        return new PredicateText(PredicateWriter.Write(probe));
    }

    public static PredicateText From(AccessStep.ProbeResult probeResult)
    {
        return new PredicateText(PredicateWriter.Write(probeResult));
    }

    public static PredicateText From(AccessStep.RangeEnd rangeEnd)
    {
        return new PredicateText(PredicateWriter.Write(rangeEnd));
    }

    public static PredicateText From(AccessStep.ProbeStart probeStart)
    {
        return new PredicateText(PredicateWriter.Write(probeStart));
    }

    public override string ToString()
    {
        return PredicateWriter.ToText(Tokens);
    }
}
