using InternalsViewer.Query.CallStack.Categories;

namespace InternalsViewer.Query.CallStack;

/// <summary>
/// A named starting point for a symbol search, such as a plan operator and the frames that implement it
/// </summary>
/// <remarks>
/// Built from the mappings, whose module, class and function globs use the same <c>*</c> syntax as the search, so
/// a rule's frame pattern is a search term as it stands. A symbol category is labelled <c>Category: </c> and its
/// display name, and searches every rule that assigns it. An operator is labelled with the badge the rule states,
/// or the plan operator it marks the start of when it states no badge. Whatever several rules describe, such as
/// Sort with its row, top-N and batch implementations, becomes one suggestion whose term searches each pattern,
/// joined with <see cref="Alternative"/>.
/// </remarks>
public sealed record SymbolSearchSuggestion(string Label, string Term)
{
    public const char Alternative = '|';

    public const string CategoryPrefix = "Category: ";

    public string Display => Label;

    /// <summary>
    /// Whether the suggestion is worth offering for the text typed so far, which is anything when nothing is
    /// </summary>
    public bool Matches(string text) =>
        text.Trim().Length == 0
        || Label.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)
        || Term.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The symbol categories followed by the operators
    /// </summary>
    public static IReadOnlyList<SymbolSearchSuggestion> FromMappings(CategoryMappings mappings) =>
        [.. FromCategories(mappings), .. FromOperators(mappings)];

    public static IReadOnlyList<SymbolSearchSuggestion> FromCategories(CategoryMappings mappings) =>
        Group(mappings.Rules
                      .Where(r => r.Category != SymbolCategory.Unknown)
                      .Select(r => (Label: (string?)(CategoryPrefix + CategoryName(r.Category)),
                                    Term: TermFor(r.Module, r.Class, r.Function))));

    public static IReadOnlyList<SymbolSearchSuggestion> FromOperators(CategoryMappings mappings) =>
        Group(mappings.Operators
                      .Select(r => (Label: r.Iterator ?? (r.PlanOperator.Count > 0 ? r.PlanOperator[0].Text : null),
                                    Term: TermFor(r.Module, r.Class, r.Function))));

    private static IReadOnlyList<SymbolSearchSuggestion> Group(IEnumerable<(string? Label, string Term)> suggestions) =>
        suggestions.Where(s => !string.IsNullOrWhiteSpace(s.Label) && s.Term.Length > 0)
                   .GroupBy(s => s.Label!, StringComparer.OrdinalIgnoreCase)
                   .Select(g => new SymbolSearchSuggestion(g.Key,
                                                           string.Join(Alternative,
                                                                       g.Select(s => s.Term)
                                                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                                                        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase))))
                   .OrderBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
                   .ToList();

    private static string CategoryName(SymbolCategory category) => category.GetCategoryMetadata()?.Name ?? category.ToString();

    private static string TermFor(GlobPattern module, GlobPattern className, GlobPattern function)
    {
        if (className.IsAny && function.IsAny)
        {
            return string.Empty;
        }

        var symbol = function.IsAny ? className.Text : $"{className.Text}::{function.Text}";

        return module.IsAny ? symbol : $"{module.Text}!{symbol}";
    }
}
