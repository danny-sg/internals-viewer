using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes a single heap row fetched by its row identifier, the access path a RID lookup uses
/// </summary>
/// <remarks>
/// The row identifier is left unset when the fetch is the inner side of a loop join, because each rebind takes one from the outer row.
/// </remarks>
public sealed record HeapFetchDefinition : IteratorDefinition
{
    public RowIdentifier? RowIdentifier { get; init; }
}
