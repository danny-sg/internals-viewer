using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.Iterators.Aggregation;

public class StreamAggregateIterator : IStepIterator
{
    public int IteratorId { get; set; }

    public IReadOnlyList<AccessStep> History { get; }

    public AccessStep? Current { get; }

    public bool IsComplete { get; }

    public PageAddress? CurrentPageAddress { get; }

    public AccessStrategy? Strategy { get; }

    public Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task CloseAsync()
    {
        throw new NotImplementedException();
    }
}
