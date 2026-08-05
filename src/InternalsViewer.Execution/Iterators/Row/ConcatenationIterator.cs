using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Row;

public sealed class ConcatenationIterator(IIteratorFactory factory) : IteratorBase, IMultiInputIterator
{
    public override PageAddress? CurrentPageAddress => Current?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Current?.Strategy;

    public long RowCount { get; private set; }

    public int InputNumber => Definitions.Count == 0 ? 0 : Math.Min(_index + 1, Definitions.Count);

    public int InputCount => Definitions.Count;

    public IReadOnlyList<IIterator> Inputs => _inputs;

    private readonly List<IIterator> _inputs = [];

    private IReadOnlyList<IteratorDefinition> Definitions { get; set; } = [];

    private int _index;

    private bool _pendingStart;

    private IIterator? Current => _index < _inputs.Count ? _inputs[_index] : null;

    public override async Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
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

        await PrepareAsync(context, definition, cancellationToken);

        Definitions = concatenation.Inputs;

        _index = 0;
        RowCount = 0;
        _pendingStart = true;

        var first = factory.Create(Definitions[0]);

        _inputs.Add(first);

        await first.OpenAsync(context, Definitions[0], cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return null;
        }

        if (_pendingStart)
        {
            _pendingStart = false;

            await EmitAsync(new AccessStep.InputStart(1, Definitions.Count), cancellationToken);
        }

        while (Current is { } current)
        {
            var row = await current.GetRowAsync(cancellationToken);

            if (row is not null)
            {
                RowCount++;

                await EmitAsync(new AccessStep.ConcatRow(RowCount, _index + 1) { EmittedRecord = row }, cancellationToken);

                CurrentRow = ProjectedRecord.Project(row, OutputList);

                return CurrentRow;
            }

            await current.CloseAsync();

            _index++;

            if (_index >= Definitions.Count)
            {
                break;
            }

            await EmitAsync(new AccessStep.InputStart(_index + 1, Definitions.Count), cancellationToken);

            var next = factory.Create(Definitions[_index]);

            _inputs.Add(next);

            await next.OpenAsync(Context, Definitions[_index], cancellationToken);
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
