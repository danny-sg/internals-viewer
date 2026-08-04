using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// One side of a join, an access path paired with what the join above needs to know about it
/// </summary>
/// <remarks>
/// The join columns belong here rather than on the access path because they are the parent's concern, not the input's. Keeping them apart
/// is also what lets <see cref="Source"/> be any definition, so a join can read another join.
/// </remarks>
public sealed record JoinInputDefinition(IteratorDefinition Source, IReadOnlyList<string> JoinColumns)
{
    /// <summary>
    /// Rows this side is expected to produce, used to size a hash table before the build reads anything
    /// </summary>
    public long RowEstimate { get; init; }

    public ScanDirection Direction { get; init; } = ScanDirection.Forward;
}
