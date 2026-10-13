using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.CallStack;

public class ResolvedCallstackFrameParser
{
    /// <summary>
    /// Parses a callstack frame string into a ResolvedCallstackFrame including the module, class name, method name, offset, and category
    /// information
    /// </summary>
    public static ResolvedCallstackFrame Parse(string module, string value)
    {
        SymbolsHelper.SplitOffset(value, out var symbolPart, out var offset);

        var separator = SymbolsHelper.FindClassMethodSeparator(symbolPart);

        var className = separator >= 0
                        ? symbolPart[..separator].Replace("`", string.Empty).Replace("'", string.Empty)
                        : null;

        var methodName = separator >= 0 ? symbolPart[(separator + 2)..] : symbolPart;

        var mappings = CategoryMappings.Default;

        var (iterator, planOperator) = mappings.ClassifyOperator(module, className, methodName);

        return new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = methodName,
            Module = module,
            Offset = offset,
            RawSymbol = value,
            ModuleCategory = mappings.GetModuleCategory(module),
            SymbolCategory = mappings.Classify(module, className, methodName),
            Iterator = iterator,
            PlanOperator = planOperator,
            IsAccessBarrier = mappings.IsAccessBarrier(module, className, methodName)
        };
    }
}