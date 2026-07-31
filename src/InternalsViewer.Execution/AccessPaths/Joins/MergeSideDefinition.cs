using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.AccessPaths.Joins;

/// <summary>
/// Describes one side of a merge join, an ordered access path with the columns it joins on
/// </summary>
public sealed record MergeSideDefinition(long AllocationUnitId,
                                         PageAddress RootPage,
                                         IReadOnlyList<SeekBounds> Ranges,
                                         IReadOnlyList<string> JoinColumns)
    : RangeDefinition(AllocationUnitId, RootPage, Ranges);
