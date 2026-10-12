using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans;

/// <summary>
/// Formats the predicates a plan operator applies
/// </summary>
/// <remarks>
/// The plan holds the model and the renderer holds the styling, so the translation between them lives here rather than in either, which
/// keeps a view free of plan structure and the parser free of presentation.
/// </remarks>
public static class PlanNodePredicateText
{
    /// <summary>
    /// Formats the condition the operator uses to reach its rows
    /// </summary>
    /// <remarks>
    /// A seek is described by its key ranges and a scan has none, so the presence of bounds decides which of the two is written and a scan
    /// falls back to the predicate it filters with.
    /// </remarks>
    public static PredicateText GetText(this PlanNode node)
    {
        return node.PredicateInfo is { HasSeekBounds: true }
            ? node.GetSeekText()
            : node.GetResidualText();
    }

    /// <summary>
    /// Formats the operator's seek ranges as a single condition
    /// </summary>
    /// <remarks>
    /// Several ranges on one operator are alternatives, as an IN list produces, so they are joined with OR to read the way the seek
    /// behaves.
    /// </remarks>
    public static PredicateText GetSeekText(this PlanNode node)
    {
        if (node.PredicateInfo is not { HasSeekBounds: true } info)
        {
            return PredicateText.Empty;
        }

        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        var isCompound = info.SeekBounds.Length > 1;

        foreach (var bounds in info.SeekBounds)
        {
            if (tokens.Count > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Space, " "));
                tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "OR"));
                tokens.Add(new PredicateToken(PredicateTokenType.Space, " "));
            }

            if (isCompound)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, "("));
            }

            tokens.AddRange(PredicateWriter.Write(bounds));

            if (isCompound)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ")"));
            }
        }

        return new PredicateText(tokens.ToImmutable());
    }

    /// <summary>
    /// Formats the predicate applied to the rows the operator returned
    /// </summary>
    public static PredicateText GetResidualText(this PlanNode node)
    {
        return node.PredicateInfo?.Residual is { } residual
            ? PredicateText.From(residual)
            : PredicateText.Empty;
    }

    public static bool HasRedundantResidual(this PlanNode node)
    {
        if (node.PredicateInfo is not { HasSeekBounds: true, Residual: not null })
        {
            return false;
        }

        var seek = Normalize(node.GetSeekText());

        var residual = Normalize(node.GetResidualText());

        return seek.Length > 0 && seek == residual;
    }

    private static string Normalize(PredicateText text)
    {
        return PredicateWriter.ToText(text.Tokens)
                   .Replace("(", string.Empty)
                   .Replace(")", string.Empty)
                   .Replace("  ", " ")
                   .Trim();
    }
}
