using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Vectors;

namespace InternalsViewer.Execution.Interfaces.BatchMode;

/// <summary>
/// Batch mode iterator
/// </summary>
public interface IBatchIterator
{
    int NodeId { get; }

    bool IsComplete { get; }

    StopReason? StopReason { get; }

    Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken);

    Task<ExecutionBatch?> GetNextBatchAsync(CancellationToken cancellationToken);

    Task CloseAsync();
}
