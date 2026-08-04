using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes an ordered access path read in key order, an index seek or scan
/// </summary>
public record RangeDefinition(long AllocationUnitId, PageAddress RootPage, IReadOnlyList<SeekBounds> Ranges) : IteratorDefinition
{
    public ScanDirection Direction { get; init; } = ScanDirection.Forward;
}
