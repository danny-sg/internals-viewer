namespace InternalsViewer.Query.CallStack.Categories;

/// <summary>
/// A module/class/function glob triple, matched against a frame
/// </summary>
/// <remarks>
/// The matching half of <see cref="SymbolCategoryRule"/> without the classification: a rule that only needs to say
/// whether a frame is one of a set, rather than what it is.
/// </remarks>
public sealed record FramePattern
{
    public required GlobPattern Module { get; init; }

    public required GlobPattern Class { get; init; }

    public required GlobPattern Function { get; init; }

    public bool Matches(string? module, string? className, string? methodName)
        => Module.Matches(module) && Class.Matches(className) && Function.Matches(methodName);
}
