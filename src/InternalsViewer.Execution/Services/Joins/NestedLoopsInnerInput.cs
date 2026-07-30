using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.Services.Joins;

/// <summary>
/// Describes the inner side of a nested loops join, the access path re-executed with bound seek values for each outer row
/// </summary>
public sealed record NestedLoopsInnerInput(long AllocationUnitId, PageAddress RootPage, IReadOnlyList<CorrelationBinding> Bindings)
{
    public AccessPredicate? Residual { get; init; }

    public long? RowGoal { get; init; }
}
