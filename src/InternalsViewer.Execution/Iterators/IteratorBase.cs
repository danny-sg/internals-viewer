using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
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

    private bool HasOpened { get; set; }

    private bool IsCloseEmitted { get; set; }

    public abstract Task OpenAsync(IteratorDefinition definition, 
                                   IteratorContext context,
                                   CancellationToken cancellationToken);

    public abstract ValueTask<IRecord?> GetRowAsync(CancellationToken cancellationToken);

    public virtual async Task CloseAsync()
    {
        await EmitCloseAsync();

        IsComplete = true;
    }

    protected void Prepare(IteratorDefinition definition, IteratorContext context)
    {
        Context = context;
        NodeId = definition.NodeId;
        OutputList = definition.OutputList;
        CurrentRow = null;
        IsComplete = false;
        StopReason = null;
    }

    protected async ValueTask PrepareAsync(IteratorDefinition definition,
                                           IteratorContext context,
                                           CancellationToken cancellationToken)
    {
        Prepare(definition, context);

        if (!HasOpened)
        {
            HasOpened = true;

            await Context.Steps.EmitAsync(new AccessStep.Open { NodeId = NodeId }, cancellationToken);
        }
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

    protected async ValueTask EmitCloseAsync()
    {
        if (!HasOpened || IsCloseEmitted)
        {
            return;
        }
        
        IsCloseEmitted = true;

        await Context.Steps.EmitAsync(new AccessStep.Close { NodeId = NodeId }, CancellationToken.None);
    }
}
