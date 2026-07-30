using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.Services.Joins;

/// <summary>
/// Describes the outer side of a nested loops join, the access path whose rows drive the rebinds
/// </summary>
public sealed record NestedLoopsOuterInput(long AllocationUnitId, PageAddress RootPage, IReadOnlyList<SeekBounds> Ranges)
{
    public AccessPredicate? Residual { get; init; }

    public ScanDirection Direction { get; init; } = ScanDirection.Forward;

    public long? RowGoal { get; init; }

    public bool HasUntranslatedResidual { get; init; }
}
