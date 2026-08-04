using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Interfaces;

public interface IStepIterator
{
    /// <summary>
    /// Identifies this operator on the steps it produces
    /// </summary>
    /// <remarks>
    /// A step is stamped once, by whatever produced it, and an operator reading from another leaves the steps it passes on alone. Composing
    /// operators therefore needs ids that are unique across the whole tree, which is what the plan node id gives.
    /// </remarks>
    int IteratorId { get; set; }

    IReadOnlyList<AccessStep> History { get; }

    AccessStep? Current { get; }

    bool IsComplete { get; }

    PageAddress? CurrentPageAddress { get; }

    AccessStrategy? Strategy { get; }

    /// <summary>
    /// Starts a walk, closing one already in progress
    /// </summary>
    /// <remarks>
    /// Every iterator opens the same way so that one can be composed into another without the caller knowing which it holds. Each narrows
    /// the definition to the shape it understands with <see cref="IteratorDefinition.Expect{T}"/>, and being handed the wrong one is a
    /// build error in whatever assembled the tree rather than something to recover from.
    /// </remarks>
    Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// The row this iterator handed upwards in a step, or null when the step is work it did rather than a row it produced
    /// </summary>
    /// <remarks>
    /// Each iterator answers only for its own steps, so an operator reading another gets that operator's results and not the reads that
    /// went into them. Without the identity test a nested operator's leaf rows would be taken as the parent's input, which is the same row
    /// stream the operator was there to transform.
    /// </remarks>
    IRecord? GetRow(AccessStep step);

    Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Releases what the walk was holding, cascading to any inputs
    /// </summary>
    /// <remarks>
    /// Opening an operator again closes it first, so this only has to be called outright when a walk is abandoned part way through.
    /// </remarks>
    Task CloseAsync();
}
