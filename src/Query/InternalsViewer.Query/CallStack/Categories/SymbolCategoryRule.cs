namespace InternalsViewer.Query.CallStack.Categories;

/// <summary>
/// One symbol-category rule: matches a frame by module/class/function glob and assigns a category and optional iterator
/// </summary>
public sealed record SymbolCategoryRule
{
    public required GlobPattern Module { get; init; }

    public required GlobPattern Class { get; init; }

    public required GlobPattern Function { get; init; }

    public required SymbolCategory Category { get; init; }

    /// <summary>
    /// Order the rule was loaded in; a later rule (e.g. an override) wins an otherwise-exact tie
    /// </summary>
    public int DefinitionOrder { get; init; }

    /// <summary>
    /// Scores this rule against a frame, returning false when any field fails to match
    /// </summary>
    public bool TryScore(string? module, string? className, string? methodName, out RuleScore score)
    {
        if (!Module.Matches(module) || !Class.Matches(className) || !Function.Matches(methodName))
        {
            score = default;

            return false;
        }

        score = new RuleScore(Function.Score, Class.Score, Module.Score, DefinitionOrder);

        return true;
    }
}