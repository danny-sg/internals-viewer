using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Search;

public sealed record SeekStrategy
{
    public ImmutableArray<SeekStrategyPhase> Phases { get; init; } = [];

    public long? RowGoal { get; init; }

    public string? RowGoalReason { get; init; }

    public SeekBounds? Bounds { get; init; }

    public ScanDirection Direction { get; init; }

    public AccessPredicate? Residual { get; init; }

    public bool HasUntranslatedResidual { get; init; }

    public int RangeCount { get; init; }

    public IReadOnlyList<SeekBounds> Ranges { get; init; } = [];

    public IReadOnlyList<string> KeyColumns { get; init; } = [];

    public bool? IsUnique { get; init; }
}
