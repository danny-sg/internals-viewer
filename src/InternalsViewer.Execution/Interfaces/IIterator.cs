using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Interfaces;

public interface IIterator
{
    int NodeId { get; }

    IRecord? CurrentRow { get; }

    bool IsComplete { get; }

    StopReason? StopReason { get; }

    PageAddress? CurrentPageAddress { get; }

    AccessStrategy? Strategy { get; }

    Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken);

    Task<IRecord?> GetRowAsync(CancellationToken cancellationToken);

    Task CloseAsync();
}
