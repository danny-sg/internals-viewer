using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes an access path re-executed with seek values bound from another row, the inner side of a nested loops join
/// </summary>
public sealed record SeekDefinition(long AllocationUnitId, PageAddress RootPage, IReadOnlyList<CorrelationBinding> Bindings)
    : IteratorDefinition;
