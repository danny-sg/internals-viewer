using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators.Indexes;

namespace InternalsViewer.Execution.Iterators.Joins.Inputs;

/// <summary>
/// An input that walks an index in key order, read straight through rather than restarted
/// </summary>
public sealed class IndexRangeJoinInput(IndexStepIterator service, RangeDefinition definition) : JoinInput
{
    public override IStepIterator Service => service;

    public override AccessStrategy? Strategy => service.Strategy;

    public Task OpenAsync(IteratorContext context, CancellationToken cancellationToken)
        => service.OpenAsync(context, definition, cancellationToken);
}
