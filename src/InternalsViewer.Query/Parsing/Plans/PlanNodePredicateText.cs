using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;

namespace InternalsViewer.Query.Parsing.Plans;

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

        foreach (var bounds in info.SeekBounds)
        {
            if (tokens.Count > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenKind.Space, " "));
                tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, "OR"));
                tokens.Add(new PredicateToken(PredicateTokenKind.Space, " "));
            }

            tokens.AddRange(PredicateWriter.Write(bounds));
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
}
