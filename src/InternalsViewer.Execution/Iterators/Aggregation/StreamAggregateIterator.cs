using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Aggregation;

public class StreamAggregateIterator : IteratorBase
{
    public override PageAddress? CurrentPageAddress => null;

    public override AccessStrategy? Strategy => null;

    public override Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
