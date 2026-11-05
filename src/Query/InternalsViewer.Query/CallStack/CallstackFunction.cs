using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.CallStack;

public sealed class CallstackFunction
{
    public string Name { get; init; } = string.Empty;

    public string? ClassName { get; init; }

    public string MethodName { get; init; } = string.Empty;

    public SymbolCategory SymbolCategory { get; init; }

    public string? Iterator { get; init; }

    public IReadOnlyList<GlobPattern> PlanOperator { get; init; } = [];

    public bool IsAccessBarrier { get; init; }

    public string? Signature { get; set; }

    public CategoryAttribute? SymbolMetadata => SymbolCategory.GetCategoryMetadata();

    public static CallstackFunction Parse(string module, string name)
    {
        var separator = SymbolsHelper.FindClassMethodSeparator(name);

        var className = separator >= 0
                        ? name[..separator].Replace("`", string.Empty).Replace("'", string.Empty)
                        : null;

        var methodName = separator >= 0 ? name[(separator + 2)..] : name;

        var mappings = CategoryMappings.Default;

        var (iterator, planOperator) = mappings.ClassifyOperator(module, className, methodName);

        return new CallstackFunction
        {
            Name = name,
            ClassName = className,
            MethodName = methodName,
            SymbolCategory = mappings.Classify(module, className, methodName),
            Iterator = iterator,
            PlanOperator = planOperator,
            IsAccessBarrier = mappings.IsAccessBarrier(module, className, methodName)
        };
    }
}
