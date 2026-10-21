using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class Instrumentation
{
    public static bool IsExcluded(SymbolCategory category, string? iterator)
        => category is SymbolCategory.XEventInfrastructure || (category is SymbolCategory.Tracing && iterator is null);

    public static bool IsExcluded(CallStackNode node) => node.IsExtendedEvents || (node.IsTracing && !node.HasOperator);
}
