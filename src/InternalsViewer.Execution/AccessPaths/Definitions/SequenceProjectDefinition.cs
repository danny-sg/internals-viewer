using InternalsViewer.Execution.AccessPaths.Windowing;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Sequence Project, which turns the flags a Segment below it set into ranking values
/// </summary>
/// <remarks>
/// The showplan Sequence element names the function but never says which flag column feeds it, so the two columns are resolved from the
/// Segments underneath while the plan is still to hand. <see cref="PartitionColumn"/> is the coarsest of them and restarts the numbering,
/// <see cref="ValueColumn"/> is the finest and marks a change in the ordering columns, which is what separates RANK from ROW_NUMBER. A
/// plan that segments only once uses the same column for both.
/// </remarks>
public sealed record SequenceProjectDefinition(IteratorDefinition Source) : UnaryDefinition(Source)
{
    public IReadOnlyList<RankingColumn> Columns { get; init; } = [];

    public string? PartitionColumn { get; init; }

    public string? ValueColumn { get; init; }
}
