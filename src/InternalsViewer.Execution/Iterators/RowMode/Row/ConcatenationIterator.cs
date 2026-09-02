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
/// Concatenation Operator
/// </summary>
/// <remarks>
/// Concatenation combines multiple inputs into a single output.
///
/// Inputs are read sequentially and outputted in read order.
/// </remarks>
public sealed class ConcatenationIterator(IIteratorFactory factory) : IteratorBase, IMultiInputIterator
{
    private readonly List<IIterator> _inputs = [];

    public override PageAddress? CurrentPageAddress => Current?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Current?.Strategy;

    public long RowCount { get; private set; }

    public int InputNumber => Definitions.Count == 0 ? 0 : Math.Min(Index + 1, Definitions.Count);

    public int InputCount => Definitions.Count;

    public IReadOnlyList<IIterator> Inputs => _inputs;

    private IReadOnlyList<IteratorDefinition> Definitions { get; set; } = [];

    private int Index { get; set; }

    private bool IsStartPending { get; set; }

    private IIterator? Current => Index < _inputs.Count ? _inputs[Index] : null;

    public override async Task OpenAsync(IteratorDefinition definition, 
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var concatenation = definition.Expect<ConcatenationDefinition>();

        if (concatenation.Inputs.Count == 0)
        {
            throw new ArgumentException("A concatenation needs at least one input");
        }

        if (_inputs.Count > 0)
        {
            await CloseAsync();

            _inputs.Clear();
        }

        await PrepareAsync(definition, context, cancellationToken);

        Definitions = concatenation.Inputs;

        Index = 0;
        RowCount = 0;
        IsStartPending = true;

        var first = factory.Create(Definitions[0]);

        _inputs.Add(first);

        await first.OpenAsync(Definitions[0], context, cancellationToken);
    }

    public override async ValueTask<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return null;
        }

        if (IsStartPending)
        {
            IsStartPending = false;

            await EmitAsync(new AccessStep.InputStart(1, Definitions.Count), cancellationToken);
        }

        while (Current is { } current)
        {
            var row = await current.GetRowAsync(cancellationToken);

            if (row is not null)
            {
                RowCount++;

                await EmitAsync(new AccessStep.ConcatRow(RowCount, Index + 1) { EmittedRecord = row }, cancellationToken);

                CurrentRow = ProjectedRecord.Project(row, OutputList);

                return CurrentRow;
            }

            await current.CloseAsync();

            Index++;

            if (Index >= Definitions.Count)
            {
                break;
            }

            await EmitAsync(new AccessStep.InputStart(Index + 1, Definitions.Count), cancellationToken);

            var next = factory.Create(Definitions[Index]);

            _inputs.Add(next);

            await next.OpenAsync(Definitions[Index], Context, cancellationToken);
        }

        CurrentRow = null;

        var reason = _inputs.Count > 0 ? _inputs[^1].StopReason : null;

        await EmitAsync(new AccessStep.Stopped(reason ?? AccessPaths.Results.StopReason.PageExhausted), cancellationToken);

        return null;
    }

    public override async Task CloseAsync()
    {
        foreach (var input in _inputs)
        {
            await input.CloseAsync();
        }

        await base.CloseAsync();
    }
}
