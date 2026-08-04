using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators;

public abstract class IteratorBase : IIterator
{
    public int NodeId { get; private set; }

    public IRecord? CurrentRow { get; protected set; }

    public bool IsComplete { get; protected set; }

    public StopReason? StopReason { get; protected set; }

    public abstract PageAddress? CurrentPageAddress { get; }

    public abstract AccessStrategy? Strategy { get; }

    protected IteratorContext Context { get; private set; } = null!;

    protected IReadOnlyList<OutputColumn> OutputList { get; private set; } = [];

    public abstract Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken);

    public abstract Task<IRecord?> GetRowAsync(CancellationToken cancellationToken);

    public virtual Task CloseAsync()
    {
        IsComplete = true;

        return Task.CompletedTask;
    }

    protected void Prepare(IteratorContext context, IteratorDefinition definition)
    {
        Context = context;
        NodeId = definition.NodeId;
        OutputList = definition.OutputList;
        CurrentRow = null;
        IsComplete = false;
        StopReason = null;
    }

    protected async ValueTask<AccessStep> EmitAsync(AccessStep step, CancellationToken cancellationToken)
    {
        var stamped = step.NodeId == NodeId ? step : step with { NodeId = NodeId };

        if (stamped is AccessStep.Stopped stopped)
        {
            StopReason = stopped.Reason;
            IsComplete = true;
        }

        await Context.Steps.EmitAsync(stamped, cancellationToken);

        return stamped;
    }
}
