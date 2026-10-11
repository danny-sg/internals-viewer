namespace InternalsViewer.Query.CallStack.Categories;

/// <summary>
/// One operator rule: matches a frame by module/class/function glob and states its badge, its plan operator, or both
/// </summary>
/// <remarks>
/// The two are stated independently and chosen independently (see <see cref="CategoryMappings.ClassifyOperator"/>), so a
/// blank cell means "not stated here" rather than "none". That is the whole reason these are not columns on
/// <see cref="SymbolCategoryRule"/>: there, one rule wins on category specificity and carries whatever else it holds, so
/// a rule added to colour a frame silently drops the boundary a broader rule had given it.
/// </remarks>
public sealed record OperatorRule
{
    public required GlobPattern Module { get; init; }

    public required GlobPattern Class { get; init; }

    public required GlobPattern Function { get; init; }

    /// <summary>
    /// The operator badge text — free display, and names things the plan has no node for; null when not stated
    /// </summary>
    public string? Iterator { get; init; }

    /// <summary>
    /// Matches the showplan <c>PhysicalOperator</c>s this frame is the entry point of; empty when not stated
    /// </summary>
    public IReadOnlyList<GlobPattern> PlanOperator { get; init; } = [];

    /// <summary>
    /// Order the rule was loaded in; a later rule (e.g. an override) wins an otherwise-exact tie
    /// </summary>
    public int DefinitionOrder { get; init; }

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
