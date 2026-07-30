using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Services.Joins;

/// <summary>
/// Describes the inner side of a nested loops join, the access path re-executed with bound seek values for each outer row
/// </summary>
public sealed record NestedLoopsInnerInput(long AllocationUnitId, PageAddress RootPage, IReadOnlyList<CorrelationBinding> Bindings)
{
    public AccessPredicate? Residual { get; init; }

    public long? RowGoal { get; init; }
}
