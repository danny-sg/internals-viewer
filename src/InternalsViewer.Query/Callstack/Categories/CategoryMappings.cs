using System.Reflection;

namespace InternalsViewer.Query.CallStack.Categories;

public sealed class CategoryMappings
{
    private readonly IReadOnlyDictionary<string, ModuleCategory> _modules;

    private readonly IReadOnlyList<SymbolCategoryRule> _rules;

    private CategoryMappings(IReadOnlyDictionary<string, ModuleCategory> modules, IReadOnlyList<SymbolCategoryRule> rules)
    {
        _modules = modules;
        _rules = rules;
    }

    public IReadOnlyList<SymbolCategoryRule> Rules => _rules;

    public ModuleCategory GetModuleCategory(string? module) =>
        !string.IsNullOrWhiteSpace(module) && _modules.TryGetValue(module, out var category)
            ? category
            : ModuleCategory.Unknown;

    public (SymbolCategory Category, string? Iterator, IReadOnlyList<GlobPattern> PlanOperator) Classify(
        string? module,
        string? className,
        string? methodName)
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

        return best is null
            ? (SymbolCategory.Unknown, null, [])
            : (best.Category, best.Iterator, best.PlanOperator);
    }

    public static CategoryMappings Load(TextReader modules,
                                        TextReader symbols,
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

        return new CategoryMappings(moduleMap, rules);
    }

    private static readonly Lazy<CategoryMappings> DefaultLazy = new(LoadDefault);

    /// <summary>
    /// The mappings loaded from the embedded core files
    /// </summary>
    public static CategoryMappings Default => DefaultLazy.Value;

    private const string ModulesFile = "ModuleCategories.txt";

    private const string SymbolsFile = "SymbolCategories.txt";

    private static CategoryMappings LoadDefault()
    {
        var assembly = typeof(CategoryMappings).Assembly;

        using var modules = OpenResource(assembly, ModulesFile);

        using var symbols = OpenResource(assembly, SymbolsFile);

        return Load(modules, symbols);
    }

    private static StreamReader OpenResource(Assembly assembly, string fileName)
    {
        var name = assembly.GetManifestResourceNames()
                           .Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        return new StreamReader(assembly.GetManifestResourceStream(name)!);
    }
}
