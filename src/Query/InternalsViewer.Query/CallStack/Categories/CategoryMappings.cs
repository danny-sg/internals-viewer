using System.Reflection;

namespace InternalsViewer.Query.CallStack.Categories;

public sealed class CategoryMappings
{
    private readonly IReadOnlyDictionary<string, ModuleCategory> _modules;

    private readonly IReadOnlyList<SymbolCategoryRule> _rules;

    private readonly IReadOnlyList<SymbolCategoryRule> _fallbacks;

    private readonly IReadOnlyList<OperatorRule> _operators;

    private readonly IReadOnlyList<FramePattern> _barriers;

    private CategoryMappings(IReadOnlyDictionary<string, ModuleCategory> modules,
                             IReadOnlyList<SymbolCategoryRule> rules,
                             IReadOnlyList<SymbolCategoryRule> fallbacks,
                             IReadOnlyList<OperatorRule> operators,
                             IReadOnlyList<FramePattern> barriers)
    {
        _modules = modules;
        _rules = rules;
        _fallbacks = fallbacks;
        _operators = operators;
        _barriers = barriers;

        RuleIndex = new RuleIndex([.. rules.Select(r => (r.Class, r.Function))]);

        FallbackIndex = new RuleIndex([.. fallbacks.Select(r => (r.Class, r.Function))]);

        OperatorIndex = new RuleIndex([.. operators.Select(o => (o.Class, o.Function))]);
    }

    /// <summary>
    /// Whether a unit of storage work begins at this frame, so an event's own stack starts here
    /// </summary>
    public bool IsAccessBarrier(string? module, string? className, string? methodName)
        => _barriers.Any(barrier => barrier.Matches(module, className, methodName));

    public IReadOnlyList<SymbolCategoryRule> Rules => _rules;

    public IReadOnlyList<OperatorRule> Operators => _operators;

    private RuleIndex RuleIndex { get; }

    private RuleIndex FallbackIndex { get; }

    private RuleIndex OperatorIndex { get; }

    public ModuleCategory GetModuleCategory(string? module) =>
        !string.IsNullOrWhiteSpace(module) && _modules.TryGetValue(module, out var category)
            ? category
            : ModuleCategory.Unknown;

    public SymbolCategory Classify(string? module, ReadOnlySpan<char> className, ReadOnlySpan<char> methodName)
        => Classify(_rules, RuleIndex, module, className, methodName)
           ?? Classify(_fallbacks, FallbackIndex, module, className, methodName)
           ?? SymbolCategory.Unknown;

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
                                                                                        ReadOnlySpan<char> className,
                                                                                        ReadOnlySpan<char> methodName)
    {
        var badge = -1;
        var boundary = -1;

        var badgeScore = default(RuleScore);
        var boundaryScore = default(RuleScore);

        foreach (var index in OperatorIndex.Candidates(className, methodName))
        {
            var rule = _operators[index];

            if (!rule.TryScore(module, className, methodName, out var score))
            {
                continue;
            }

            if (rule.Iterator is not null && Improves(index, score, badge, badgeScore))
            {
                badge = index;

                badgeScore = score;
            }

            if (rule.PlanOperator.Count > 0 && Improves(index, score, boundary, boundaryScore))
            {
                boundary = index;

                boundaryScore = score;
            }
        }

        return (badge >= 0 ? _operators[badge].Iterator : null, boundary >= 0 ? _operators[boundary].PlanOperator : []);
    }

    public static CategoryMappings Load(TextReader modules,
                                        TextReader symbols,
                                        TextReader? operators = null,
                                        TextReader? barriers = null,
                                        TextReader? overrideModules = null,
                                        TextReader? overrideSymbols = null,
                                        TextReader? fallbacks = null)
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
                                    fallbacks is null ? [] : [.. MappingParser.ParseSymbols(fallbacks)],
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

    private const string FallbacksFile = "FallbackCategories.txt";

    private static CategoryMappings LoadDefault()
    {
        var assembly = typeof(CategoryMappings).Assembly;

        using var modules = OpenResource(assembly, ModulesFile);

        using var symbols = OpenResource(assembly, SymbolsFile);

        using var operators = OpenResource(assembly, OperatorsFile);

        using var barriers = OpenResource(assembly, BarriersFile);

        using var fallbacks = OpenResource(assembly, FallbacksFile);

        return Load(modules, symbols, operators, barriers, fallbacks: fallbacks);
    }

    private static SymbolCategory? Classify(IReadOnlyList<SymbolCategoryRule> rules,
                                            RuleIndex index,
                                            string? module,
                                            ReadOnlySpan<char> className,
                                            ReadOnlySpan<char> methodName)
    {
        var best = -1;

        var bestScore = default(RuleScore);

        foreach (var candidate in index.Candidates(className, methodName))
        {
            if (rules[candidate].TryScore(module, className, methodName, out var score) && Improves(candidate, score, best, bestScore))
            {
                best = candidate;

                bestScore = score;
            }
        }

        return best >= 0 ? rules[best].Category : null;
    }

    private static bool Improves(int index, RuleScore score, int best, RuleScore bestScore)
        => best < 0 || score.IsBetterThan(bestScore) || (!bestScore.IsBetterThan(score) && index < best);

    private static StreamReader OpenResource(Assembly assembly, string fileName)
    {
        var name = assembly.GetManifestResourceNames()
                           .Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        return new StreamReader(assembly.GetManifestResourceStream(name)!);
    }
}
