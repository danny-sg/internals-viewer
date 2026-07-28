using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Search;

public sealed record SeekStrategy
{
    public ImmutableArray<SeekStrategyPhase> Phases { get; init; } = [];

    public long? RowGoal { get; init; }

    public string? RowGoalReason { get; init; }

    public SeekBounds? Bounds { get; init; }

    public ScanDirection Direction { get; init; }
}
