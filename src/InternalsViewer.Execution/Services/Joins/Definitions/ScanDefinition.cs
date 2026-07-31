using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.Services.Joins.Definitions;

/// <summary>
/// Describes an ordered access path a join reads from, the outer side of a nested loops join or either side of a merge join
/// </summary>
public record ScanDefinition(long AllocationUnitId, PageAddress RootPage, IReadOnlyList<SeekBounds> Ranges)
{
    public AccessPredicate? Residual { get; init; }

    public ScanDirection Direction { get; init; } = ScanDirection.Forward;

    public long? RowGoal { get; init; }

    public bool HasUntranslatedResidual { get; init; }
}
