using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.AccessPaths.Windowing;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.RowMode.Windowing;

/// <summary>
/// Sequence Project operator
/// </summary>
/// <remarks>
/// Sequence Project is always input by a Segment operator that appends a field with a value of 1 or 0 to flag if the partition grouping
/// has changed.
/// 
/// Ranking is not an expression over one row, it is a running count that the segment flags restart, which is why this cannot be folded
/// into a Compute Scalar. Three counters cover all three functions and every ranking column in the operator reads whichever it needs, so
/// a query ranking the same window more than once still makes a single pass.
/// </remarks>
public sealed class SequenceProjectIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator
{
    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    public IIterator? Input { get; private set; }

    public long RowCount { get; private set; }

    public IReadOnlyList<RankingColumn> Columns { get; private set; } = [];

    /// <summary>
    /// The row's position within its partition, which is what ROW_NUMBER returns
    /// </summary>
    public long PartitionRow { get; private set; }

    public long Rank { get; private set; }

    public long DenseRank { get; private set; }

    private string PartitionColumn { get; set; } = string.Empty;

    private string ValueColumn { get; set; } = string.Empty;

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var sequence = definition.Expect<SequenceProjectDefinition>();

        if (sequence.Columns.Count == 0 || sequence.PartitionColumn is null || sequence.ValueColumn is null)
        {
            throw new ArgumentException("A sequence project needs ranking columns and the segment flags that drive them");
        }

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        Columns = sequence.Columns;

        PartitionColumn = sequence.PartitionColumn;
        ValueColumn = sequence.ValueColumn;

        RowCount = 0;

        PartitionRow = 0;
        Rank = 0;
        DenseRank = 0;

        Input = factory.Create(sequence.Source);

        await Input.OpenAsync(sequence.Source, context, cancellationToken);
    }

    public override async ValueTask<IRecord?> GetRowAsync(CancellationToken cancellationToken)
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

        var source = new RecordRowValueSource(row);

        var isNewPartition = IsSet(row, source, PartitionColumn);

        var isNewValue = IsSet(row, source, ValueColumn);

        Advance(isNewPartition, isNewValue);

        var values = new List<RecordField>(Columns.Count);

        foreach (var column in Columns)
        {
            values.Add(new ComputedField(column.Column, AccessValue.FromInteger(RankingFunctions.ResultType, Value(column))));
        }

        var record = ComputedRecord.Extend(row, values);

        var step = new AccessStep.RankRow(RowCount)
        {
            EmittedRecord = record,
            Values = Text(values),
            IsNewPartition = isNewPartition,
            PartitionRow = PartitionRow
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

    /// <summary>
    /// Moves the row/rank/dense rank counters on for a row
    /// </summary>
    /// <remarks>
    /// Rank takes the row's position within the partition rather than counting up, which is what leaves the gap after a tie that dense
    /// rank does not have.
    /// </remarks>
    private void Advance(bool isNewPartition, bool isNewValue)
    {
        if (isNewPartition)
        {
            PartitionRow = 1;
            Rank = 1;
            DenseRank = 1;

            return;
        }

        PartitionRow++;

        if (!isNewValue)
        {
            return;
        }

        Rank = PartitionRow;
        DenseRank++;
    }

    private long Value(RankingColumn column)
        => column.Function switch
        {
            RankingFunction.RowNumber 
                => PartitionRow,
            RankingFunction.Rank 
                => Rank,
            _ => DenseRank
        };

    private static bool IsSet(IRecord row, RecordRowValueSource source, string column)
    {
        if (row.Fields.All(f => !string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Row has no segment column '{column}' to rank on");
        }

        var value = source.GetValue(-1, column);

        return !value.IsNull && value.Numeric != 0;
    }

    private static string Text(List<RecordField> values)
        => string.Join(", ", values.OfType<ComputedField>()
                                   .Select(f => $"{f.ColumnStructure.ColumnName} = {f.ComputedValue.Numeric}"));
}
