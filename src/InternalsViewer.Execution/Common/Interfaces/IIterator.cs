using InternalsViewer.Execution.RowMode.AccessPaths.Definitions;
using InternalsViewer.Execution.Common.AccessPaths.Results;
using InternalsViewer.Execution.Common.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Common.Interfaces;

public interface IIterator
{
    int NodeId { get; }

    IRecord? CurrentRow { get; }

    bool IsComplete { get; }

    StopReason? StopReason { get; }

    PageAddress? CurrentPageAddress { get; }

    AccessStrategy? Strategy { get; }

    Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken);

    ValueTask<IRecord?> GetRowAsync(CancellationToken cancellationToken);

    Task CloseAsync();
}
