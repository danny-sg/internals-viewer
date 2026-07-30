using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.Services.Joins;

/// <summary>
/// Describes one side of a merge join, an ordered access path with the columns it joins on
/// </summary>
public sealed record MergeJoinSideInput(long AllocationUnitId,
                                        PageAddress RootPage,
                                        IReadOnlyList<SeekBounds> Ranges,
                                        IReadOnlyList<string> JoinColumns)
{
    public AccessPredicate? Residual { get; init; }

    public ScanDirection Direction { get; init; } = ScanDirection.Forward;

    public bool HasUntranslatedResidual { get; init; }
}
