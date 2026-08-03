using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;

namespace InternalsViewer.Execution.Iterators.Joins.Inputs;

/// <summary>
/// An input read straight through rather than restarted, which is any iterator a join reads once
/// </summary>
/// <remarks>
/// Nothing here is specific to an index, so the input may itself be a join and the tree may be any depth.
/// </remarks>
public sealed class IteratorJoinInput(IStepIterator iterator, IteratorDefinition definition) : JoinInput
{
    public override IStepIterator Iterator => iterator;

    public override AccessStrategy? Strategy => iterator.Strategy;

    public Task OpenAsync(IteratorContext context, CancellationToken cancellationToken)
        => iterator.OpenAsync(context, definition, cancellationToken);
}
