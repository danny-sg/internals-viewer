namespace InternalsViewer.Query.FullTrace.Calls.Tree;

/// <summary>
/// Call frame activity as a heat band
/// </summary>
public sealed record ActivityBand(IReadOnlyList<CallTreeNode> Children);
