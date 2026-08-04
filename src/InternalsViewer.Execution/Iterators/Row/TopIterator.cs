using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Row;

/// <summary>
/// Stops reading its input once the requested number of rows has passed through
/// </summary>
public sealed class TopIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator
{
    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    /// <summary>
    /// Rows counted so far, which is what the limit is tested against
    /// </summary>
    public long RowCount { get; private set; }

    public IIterator? Input { get; private set; }

    private long Limit { get; set; }

    private bool PendingStart { get; set; }

    public override async Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
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

        Prepare(context, definition);

        Input = factory.Create(top.Source);
        Limit = top.RowCount;
        RowCount = 0;
        PendingStart = true;

        await Input.OpenAsync(context, top.Source, cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        if (PendingStart)
        {
            PendingStart = false;

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
