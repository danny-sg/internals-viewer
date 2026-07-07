using InternalsViewer.Query.Callstack.Categories;

namespace InternalsViewer.Query.Callstack;

public sealed record ResolvedCallstackFrame
{
    public string Module { get; init; } = "";

    public ModuleCategory ModuleCategory { get; init; }

    public SymbolCategory SymbolCategory { get; init; }

    public string RawSymbol { get; init; } = string.Empty;

    public string? ClassName { get; init; }

    public string MethodName { get; init; } = string.Empty;

    public uint? Offset { get; init; }

    public CategoryAttribute? ModuleMetadata => ModuleCategory.GetCategoryMetadata();

    public CategoryAttribute? SymbolMetadata => SymbolCategory.GetCategoryMetadata();

}