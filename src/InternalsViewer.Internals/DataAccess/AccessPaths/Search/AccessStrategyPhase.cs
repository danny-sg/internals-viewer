using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Search;

public sealed record AccessStrategyPhase
{
    public AccessPhase Phase { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Lead { get; init; } = string.Empty;

    public ImmutableArray<PredicateToken> LeadCondition { get; init; } = [];

    public string Middle { get; init; } = string.Empty;

    public ImmutableArray<PredicateToken> Condition { get; init; } = [];

    public string Trail { get; init; } = string.Empty;
}