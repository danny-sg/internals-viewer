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
    /// The plan operator this frame implements (e.g. Top, Hash Match), for the operator badge — null when not applicable
    /// </summary>
    public string? Iterator { get; init; }

    /// <summary>
    /// Matches the showplan <c>PhysicalOperator</c>s this frame is the entry point of; empty when the frame is not one
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Iterator"/> because the two answer different questions. Iterator is free display text
    /// and names things the plan has no node for at all (Profiling, Row Iterator, SELECT) or names a phase rather than a
    /// node (Hash Match Build/Probe, both of which are the one Hash Match operator). This names a real plan node,
    /// because it is what cuts the call tree into per-operator segments.
    ///
    /// A list of patterns, not a literal, because one class routinely serves several operators the plan names
    /// separately — and not always by related names, so a single glob will not do. CQScanRange is the Index Seek, the
    /// Clustered Index Seek AND the Key Lookup, which is the same seek that <c>ExecutionPlanParser</c> renames when the
    /// showplan marks it a lookup. Two rules identical but for this cell could not express it either: the later would
    /// simply win.
    /// </remarks>
    public IReadOnlyList<GlobPattern> PlanOperator { get; init; } = [];

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

/// <summary>
/// A matched rule's specificity — most specific wins, with function weighted highest and used as the tiebreak
/// </summary>
public readonly record struct RuleScore(int Function, int Class, int Module, int DefinitionOrder)
{
    private int Total => Function + Class + Module;

    /// <summary>
    /// Whether this match is more detailed than <paramref name="other"/> (total specificity, then function, class,
    /// module, then a later definition wins)
    /// </summary>
    public bool IsBetterThan(RuleScore other)
    {
        if (Total != other.Total)
        {
            return Total > other.Total;
        }

        if (Function != other.Function)
        {
            return Function > other.Function;
        }

        if (Class != other.Class)
        {
            return Class > other.Class;
        }

        if (Module != other.Module)
        {
            return Module > other.Module;
        }

        return DefinitionOrder > other.DefinitionOrder;
    }
}
