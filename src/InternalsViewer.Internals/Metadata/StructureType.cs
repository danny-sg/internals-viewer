
namespace InternalsViewer.Internals.Metadata;

/// <summary>
/// HOBT structure type
/// </summary>
public enum StructureType
{
    /// <summary>
    /// Heap - table without a clustered index
    /// </summary>
    Heap,
    /// <summary>
    /// B-Tree (Index)
    /// </summary>
    BTree
}