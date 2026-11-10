using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

public sealed class FrameFunction
{
    public string Name { get; init; } = string.Empty;

    public string? ClassName { get; init; }

    public string MethodName { get; init; } = string.Empty;

    public SymbolCategory SymbolCategory { get; init; }

    public string? Iterator { get; init; }

    public IReadOnlyList<GlobPattern> PlanOperator { get; init; } = [];

    public string? Signature { get; set; }

    public CategoryAttribute? SymbolMetadata => SymbolCategory.GetCategoryMetadata();

    public static FrameFunction Parse(string module, string name)
    {
        var separator = SymbolHelper.FindClassMethodSeparator(name);

        var className = separator >= 0
                        ? name[..separator].Replace("`", string.Empty).Replace("'", string.Empty)
                        : null;

        var methodName = separator >= 0 ? name[(separator + 2)..] : name;

        var mappings = CategoryMappings.Default;

        var (iterator, planOperator) = mappings.ClassifyOperator(module, className, methodName);

        return new FrameFunction
        {
            Name = name,
            ClassName = className,
            MethodName = methodName,
            SymbolCategory = mappings.Classify(module, className, methodName),
            Iterator = iterator,
            PlanOperator = planOperator
        };
    }
}
