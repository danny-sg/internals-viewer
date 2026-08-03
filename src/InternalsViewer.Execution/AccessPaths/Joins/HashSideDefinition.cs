using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.AccessPaths.Joins;

/// <summary>
/// Describes one side of a hash match, an access path with the columns it joins on
/// </summary>
public sealed record HashSideDefinition(long AllocationUnitId,
                                        PageAddress RootPage,
                                        IReadOnlyList<SeekBounds> Ranges,
                                        IReadOnlyList<string> JoinColumns)
    : RangeDefinition(AllocationUnitId, RootPage, Ranges)
{
    /// <summary>
    /// Rows this side is expected to produce, used to size the hash table before the build reads anything
    /// </summary>
    public long RowEstimate { get; init; }
}
