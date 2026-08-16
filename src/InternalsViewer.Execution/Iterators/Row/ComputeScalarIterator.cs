using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Row;

public sealed class ComputeScalarIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator
{
    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    public IIterator? Input { get; private set; }

    public long RowCount { get; private set; }

    public IReadOnlyList<ComputedColumn> Columns { get; private set; } = [];

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var compute = definition.Expect<ComputeScalarDefinition>();

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        Columns = compute.Columns;

        RowCount = 0;

        Input = factory.Create(compute.Source);

        await Input.OpenAsync(compute.Source, context, cancellationToken);
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

        var computed = Compute(row);

        var record = computed.Count == 0 ? row : ComputedRecord.Extend(row, computed);

        var step = new AccessStep.ComputeRow(RowCount)
        {
            EmittedRecord = record,
            Values = Text(computed)
        };

        await EmitAsync(step, cancellationToken);

        CurrentRow = ProjectedRecord.Project(record, OutputList);

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

    private List<RecordField> Compute(IRecord row)
    {
        var fields = new List<RecordField>(Columns.Count);

        if (Columns.Count == 0)
        {
            return fields;
        }

        var source = new RecordRowValueSource(row);

        foreach (var column in Columns)
        {
            var value = PredicateEvaluator.Resolve(column.Expression, source, Context.EvaluationContext);

            if (column.DataType is { } dataType)
            {
                value = AccessValueConverter.ConvertTo(value, dataType);
            }

            fields.Add(new ComputedField(column.Name, value));
        }

        return fields;
    }

    private static string Text(List<RecordField> computed)
        => string.Join(", ", computed.OfType<ComputedField>()
                                     .Select(f => $"{f.ColumnStructure.ColumnName} = {AccessValueFormatter.ToText(f.ComputedValue)}"));
}
