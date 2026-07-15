using System.Reflection;

namespace InternalsViewer.Query.CallStack.Categories;

public sealed class CategoryMappings
{
    private readonly IReadOnlyDictionary<string, ModuleCategory> _modules;

    private readonly IReadOnlyList<SymbolCategoryRule> _rules;

    private readonly IReadOnlyList<OperatorRule> _operators;

    private readonly IReadOnlyList<FramePattern> _barriers;

    private CategoryMappings(IReadOnlyDictionary<string, ModuleCategory> modules,
                             IReadOnlyList<SymbolCategoryRule> rules,
                             IReadOnlyList<OperatorRule> operators,
                             IReadOnlyList<FramePattern> barriers)
    {
        _modules = modules;
        _rules = rules;
        _operators = operators;
        _barriers = barriers;
    }

    /// <summary>
    /// Whether a unit of storage work begins at this frame, so an event's own stack starts here
    /// </summary>
    public bool IsAccessBarrier(string? module, string? className, string? methodName)
        => _barriers.Any(barrier => barrier.Matches(module, className, methodName));

    public IReadOnlyList<SymbolCategoryRule> Rules => _rules;

    public ModuleCategory GetModuleCategory(string? module) =>
        !string.IsNullOrWhiteSpace(module) && _modules.TryGetValue(module, out var category)
            ? category
            : ModuleCategory.Unknown;

    public SymbolCategory Classify(string? module, string? className, string? methodName)
    {
        SymbolCategoryRule? best = null;

        var bestScore = default(RuleScore);

        foreach (var rule in _rules)
        {
            if (rule.TryScore(module, className, methodName, out var score) && (best is null || score.IsBetterThan(bestScore)))
            {
                best = rule;

                bestScore = score;
            }
        }

        return best?.Category ?? SymbolCategory.Unknown;
    }

    /// <summary>
    /// The plan operator a frame belongs to: its badge, and the operators it is the entry point of
    /// </summary>
    /// <remarks>
    /// Each is taken from the most specific rule that actually STATES it, independently of the other. A blank cell means
    /// "not stated here", so a specific rule can rename the badge and still inherit the boundary from a general one:
    /// CQScanHash::ConsumeProbe is badged "Hash Match Probe" by its own rule while CQScanHash* supplies the Hash Match
    /// boundary, rather than every rule having to repeat it.
    ///
    /// One contest per field is the point of this being its own file. Sharing SymbolCategories.txt' contest meant the
    /// rule that won on CATEGORY specificity carried the boundary, so adding a rule to colour a frame could delete one.
    /// </remarks>
    public (string? Iterator, IReadOnlyList<GlobPattern> PlanOperator) ClassifyOperator(string? module,
                                                                                        string? className,
                                                                                        string? methodName)
    {
        OperatorRule? badge = null;
        OperatorRule? boundary = null;

        var badgeScore = default(RuleScore);
        var boundaryScore = default(RuleScore);

        foreach (var rule in _operators)
        {
            if (!rule.TryScore(module, className, methodName, out var score))
            {
                continue;
            }

            if (rule.Iterator is not null && (badge is null || score.IsBetterThan(badgeScore)))
            {
                badge = rule;

                badgeScore = score;
            }

            if (rule.PlanOperator.Count > 0 && (boundary is null || score.IsBetterThan(boundaryScore)))
            {
                boundary = rule;

                boundaryScore = score;
            }
        }

        return (badge?.Iterator, boundary?.PlanOperator ?? []);
    }

    public static CategoryMappings Load(TextReader modules,
                                        TextReader symbols,
                                        TextReader? operators = null,
                                        TextReader? barriers = null,
                                        TextReader? overrideModules = null,
                                        TextReader? overrideSymbols = null)
    {
        var moduleMap = new Dictionary<string, ModuleCategory>(StringComparer.OrdinalIgnoreCase);

        foreach (var (module, category) in MappingParser.ParseModules(modules))
        {
            moduleMap[module] = category;
        }

        var rules = MappingParser.ParseSymbols(symbols).ToList();

        if (overrideModules is not null)
        {
            // A later download can correct a module's category.
            foreach (var (module, category) in MappingParser.ParseModules(overrideModules))
            {
                moduleMap[module] = category;
            }
        }

        if (overrideSymbols is not null)
        {
            // Appended after the core rules, so a same-specificity override rule wins the tie.
            rules.AddRange(MappingParser.ParseSymbols(overrideSymbols, startOrder: rules.Count));
        }

        return new CategoryMappings(moduleMap,
                                    rules,
                                    operators is null ? [] : [.. MappingParser.ParseOperators(operators)],
                                    barriers is null ? [] : [.. MappingParser.ParseBarriers(barriers)]);
    }

    private static readonly Lazy<CategoryMappings> DefaultLazy = new(LoadDefault);

    /// <summary>
    /// The mappings loaded from the embedded core files
    /// </summary>
    public static CategoryMappings Default => DefaultLazy.Value;

    private const string ModulesFile = "ModuleCategories.txt";

    private const string SymbolsFile = "SymbolCategories.txt";

    private const string OperatorsFile = "Operators.txt";

    private const string BarriersFile = "Barriers.txt";

    private static CategoryMappings LoadDefault()
    {
        var assembly = typeof(CategoryMappings).Assembly;

        using var modules = OpenResource(assembly, ModulesFile);

        using var symbols = OpenResource(assembly, SymbolsFile);

        using var operators = OpenResource(assembly, OperatorsFile);

        using var barriers = OpenResource(assembly, BarriersFile);

        return Load(modules, symbols, operators, barriers);
    }

    private static StreamReader OpenResource(Assembly assembly, string fileName)
    {
        var name = assembly.GetManifestResourceNames()
                           .Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        return new StreamReader(assembly.GetManifestResourceStream(name)!);
    }
}
