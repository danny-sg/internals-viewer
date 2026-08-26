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
/// Select Iterator
/// </summary>
/// <remarks>
/// Pass through iterator to project input into the final output
/// </remarks>
public sealed class SelectIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator
{
    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    public long RowCount { get; private set; }

    public IIterator? Input { get; private set; }

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var select = definition.Expect<SelectDefinition>();

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        Input = factory.Create(select.Source);
        RowCount = 0;

        await Input.OpenAsync(select.Source, context, cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        var row = await Input.GetRowAsync(cancellationToken);

        if (row is null)
        {
            CurrentRow = null;

            await EmitAsync(new AccessStep.Stopped(Input.StopReason ?? AccessPaths.Results.StopReason.PageExhausted), 
                            cancellationToken);

            return null;
        }

        RowCount++;

        await EmitAsync(new AccessStep.Output(RowCount) { EmittedRecord = row }, cancellationToken);

        CurrentRow = ProjectedRecord.Project(row, OutputList);

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
