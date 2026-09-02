using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.RowMode.Row;

/// <summary>
/// Top Operator iterator
/// </summary>
/// <remarks>
/// Streaming operator that will read and pass through rows, counting them as it goes and will stop when it hits the limit set.
/// </remarks>
public sealed class TopIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator
{
    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    /// <summary>
    /// Current row count
    /// </summary>
    public long RowCount { get; private set; }

    public IIterator? Input { get; private set; }

    public long Limit { get; private set; }

    private bool IsPendingStart { get; set; }

    public override async Task OpenAsync(IteratorDefinition definition, 
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var top = definition.Expect<TopDefinition>();

        if (top.IsPercent)
        {
            throw new ArgumentException("A percentage TOP is not simulated, because the input has to be counted before the limit is known");
        }

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        Input = factory.Create(top.Source);
        Limit = top.RowCount;
        RowCount = 0;
        IsPendingStart = true;

        await Input.OpenAsync(top.Source, context, cancellationToken);
    }

    public override async ValueTask<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        if (IsPendingStart)
        {
            IsPendingStart = false;

            await EmitAsync(new AccessStep.TopStart(Limit), cancellationToken);
        }

        var row = await Input.GetRowAsync(cancellationToken);

        if (row is null)
        {
            IsComplete = true;
            StopReason = Input.StopReason;
            CurrentRow = null;

            return null;
        }

        RowCount++;

        await EmitAsync(new AccessStep.TopRow(RowCount, Limit) { EmittedRecord = row }, cancellationToken);

        CurrentRow = ProjectedRecord.Project(row, OutputList);

        if (RowCount >= Limit)
        {
            await EmitAsync(new AccessStep.Stopped(AccessPaths.Results.StopReason.RowGoalMet), cancellationToken);

            await Input.CloseAsync();
        }

        return CurrentRow;
    }

    public override async Task CloseAsync()
    {
        if (Input is not null)
        {
            await Input.CloseAsync();
        }

        await base.CloseAsync();
    }
}
