using InternalsViewer.Query.CallStack.Categories;

namespace InternalsViewer.Query.CallStack;

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

    /// <summary>
    /// Matches the showplan <c>PhysicalOperator</c>s this frame is the entry point of; empty when the frame is not one
    /// </summary>
    /// <remarks>
    /// Frames carrying these cut the call tree into per-operator segments. See
    /// <see cref="Categories.SymbolCategoryRule.PlanOperator"/> for why it is a list of patterns, and why it is not
    /// <see cref="Iterator"/>.
    /// </remarks>
    public IReadOnlyList<GlobPattern> PlanOperator { get; init; } = [];

    /// <summary>
    /// Whether a unit of storage work begins here, so an individual event's own stack starts at this frame
    /// </summary>
    /// <remarks>
    /// Where <see cref="PlanOperator"/> bounds an operator's segment, this bounds one event's: selecting a read shows
    /// the work that read did, with the operator's iteration machinery above it cut away.
    /// </remarks>
    public bool IsAccessBarrier { get; init; }

    public string? ClassName { get; init; }

    public string MethodName { get; init; } = string.Empty;

    public uint? Offset { get; init; }

    public CategoryAttribute? ModuleMetadata => ModuleCategory.GetCategoryMetadata();

    public CategoryAttribute? SymbolMetadata => SymbolCategory.GetCategoryMetadata();
}