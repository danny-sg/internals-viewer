namespace InternalsViewer.Internals.Engine.Allocation.Enums;

/// <summary>
/// Allocation chain type
/// </summary>
public enum ChainType
{
    /// <summary>
    /// If the next page in the chain is based on a bitmap interval
    /// </summary>
    Interval,
    /// <summary>
    /// If the next page in the chain is based on a linked list in the Page Header
    /// </summary>
    Linked
}