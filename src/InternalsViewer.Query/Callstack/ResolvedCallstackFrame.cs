using InternalsViewer.Query.Callstack.Categories;

namespace InternalsViewer.Query.Callstack;

public sealed record ResolvedCallstackFrame
{
    public string Module { get; init; } = "";

    public ModuleCategory ModuleCategory { get; init; }

    public SymbolCategory SymbolCategory { get; init; }

    public string RawSymbol { get; init; } = string.Empty;

    /// <summary>
    /// The plan operator this frame implements if it is a query iterator (e.g. Top, Hash Match), otherwise null
    /// </summary>
    public string? Iterator { get; init; }

    public string? ClassName { get; init; }

    public string MethodName { get; init; } = string.Empty;

    public uint? Offset { get; init; }

    public CategoryAttribute? ModuleMetadata => ModuleCategory.GetCategoryMetadata();

    public CategoryAttribute? SymbolMetadata => SymbolCategory.GetCategoryMetadata();
}