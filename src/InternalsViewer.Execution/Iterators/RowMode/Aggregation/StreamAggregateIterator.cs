using System.Runtime.InteropServices;
using InternalsViewer.Execution.AccessPaths.Aggregation;
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

namespace InternalsViewer.Execution.Iterators.RowMode.Aggregation;

/// <summary>
/// Stream Aggregate Operator
/// </summary>
/// <remarks>
/// Stream Aggregate relies on the input being sorted by the key columns.
///
/// The current key is tracked. If the key changes, the current group is emitted and a new group is started. If the key doesn't change the
/// row is accumulated into the current group.
///
/// It provides a memory efficient aggregation as unlike the hash aggregate it only needs to hold one group in memory and can emit results
/// as soon as the group is complete. The signal for completion is implicit as the operators can rely on the fact that the sorted input
/// means it will not see any more rows for the current group in any future row.
/// </remarks>
public sealed class StreamAggregateIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator
{
    private readonly List<AggregateAccumulator> _accumulators = [];

    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    public IIterator? Input { get; private set; }

    public IReadOnlyList<string> GroupBy { get; private set; } = [];

    public IReadOnlyList<AggregateColumn> Aggregates { get; private set; } = [];

    public IReadOnlyList<AggregateValue> Running => [.. _accumulators.Select(a => a.Value)];

    public IReadOnlyList<AggregateValue> GroupValues => AggregateGroupValues.Of(GroupBy, GroupKey);

    public long RowCount { get; private set; }

    public long InputRowCount { get; private set; }

    public long GroupRowCount { get; private set; }

    public string CurrentKey { get; private set; } = string.Empty;

    private AccessKey GroupKey { get; set; }

    private IRecord? GroupRow { get; set; }

    private IRecord? PendingRow { get; set; }

    private bool HasGroup { get; set; }

    private bool IsInputDone { get; set; }

    private bool IsPendingStart { get; set; }

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var aggregate = definition.Expect<StreamAggregateDefinition>();

        if (aggregate.Aggregates.Count == 0 && aggregate.GroupBy.Count == 0)
        {
            throw new ArgumentException("A stream aggregate needs at least one aggregate or grouping column");
        }

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        GroupBy = aggregate.GroupBy;
        Aggregates = aggregate.Aggregates;

        _accumulators.Clear();
        _accumulators.AddRange(aggregate.Aggregates.Select(a => new AggregateAccumulator(a)));

        RowCount = 0;
        InputRowCount = 0;
        GroupRowCount = 0;

        CurrentKey = string.Empty;
        GroupKey = AccessKey.Unbounded;
        GroupRow = null;
        PendingRow = null;

        HasGroup = false;
        IsInputDone = false;
        IsPendingStart = true;

        Input = factory.Create(aggregate.Source);

        await Input.OpenAsync(aggregate.Source, context, cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        if (IsPendingStart)
        {
            IsPendingStart = false;

            var start = new AccessStep.AggregateStart(GroupBy.Count == 0)
            {
                Aggregates = string.Join(", ", Aggregates.Select(a => a.ToText())),
                GroupBy = string.Join(", ", GroupBy)
            };

            await EmitAsync(start, cancellationToken);
        }

        while (!IsInputDone)
        {
            var row = await NextRowAsync(cancellationToken);

            if (row is null)
            {
                IsInputDone = true;

                break;
            }

            var key = GetKey(row);

            if (HasGroup && GroupBy.Count > 0 && !key.Equals(GroupKey))
            {
                PendingRow = row;

                return await EmitGroupAsync(cancellationToken);
            }

            if (!HasGroup)
            {
                await StartGroupAsync(key, row, cancellationToken);
            }

            Accumulate(row);

            InputRowCount++;
            GroupRowCount++;

            var accumulated = new AccessStep.AggregateRow(InputRowCount, GroupRowCount)
            {
                EmittedRecord = row,
                Running = RunningText()
            };

            await EmitAsync(accumulated, cancellationToken);
        }

        if (HasGroup)
        {
            return await EmitGroupAsync(cancellationToken);
        }

        if (GroupBy.Count == 0 && RowCount == 0)
        {
            await StartGroupAsync(AccessKey.Unbounded, null, cancellationToken);

            return await EmitGroupAsync(cancellationToken);
        }

        CurrentRow = null;

        await EmitAsync(new AccessStep.Stopped(Input.StopReason ?? AccessPaths.Results.StopReason.PageExhausted),
                        cancellationToken);

        return null;
    }

    public override async Task CloseAsync()
    {
        if (Input is not null)
        {
            await Input.CloseAsync();
        }

        await base.CloseAsync();
    }

    private async Task<IRecord?> NextRowAsync(CancellationToken cancellationToken)
    {
        if (PendingRow is not { } pending)
        {
            return await Input!.GetRowAsync(cancellationToken);
        }

        PendingRow = null;

        return pending;
    }

    private async Task StartGroupAsync(AccessKey key, IRecord? row, CancellationToken cancellationToken)
    {
        HasGroup = true;

        GroupKey = key;
        GroupRow = row;
        GroupRowCount = 0;

        CurrentKey = KeyText(key);

        foreach (var accumulator in _accumulators)
        {
            accumulator.Reset();
        }

        await EmitAsync(new AccessStep.AggregateGroup(RowCount + 1, CurrentKey), cancellationToken);
    }

    private async Task<IRecord> EmitGroupAsync(CancellationToken cancellationToken)
    {
        RowCount++;

        var record = BuildRecord();

        var groupRows = GroupRowCount;

        HasGroup = false;

        CurrentRow = ProjectedRecord.Project(record, OutputList);

        var emit = new AccessStep.AggregateEmit(RowCount, CurrentKey)
        {
            EmittedRecord = record,
            Values = RunningText(),
            GroupRows = groupRows
        };

        await EmitAsync(emit, cancellationToken);

        return CurrentRow;
    }

    private ComputedRecord BuildRecord()
    {
        var fields = new List<RecordField>(GroupBy.Count + _accumulators.Count);

        foreach (var column in GroupBy)
        {
            if (FindField(GroupRow, column) is { } field)
            {
                fields.Add(field);
            }
        }

        foreach (var accumulator in _accumulators)
        {
            fields.Add(new ComputedField(accumulator.Column.Column, accumulator.Result));
        }

        return ComputedRecord.Create(fields);
    }

    private void Accumulate(IRecord row)
    {
        if (_accumulators.Count == 0)
        {
            return;
        }

        var source = new RecordRowValueSource(row);

        foreach (var accumulator in _accumulators)
        {
            var value = accumulator.Column.Argument is { } argument
                        ? PredicateEvaluator.Resolve(argument, source, Context.EvaluationContext)
                        : AccessValue.Null;

            accumulator.Add(value);
        }
    }

    private AccessKey GetKey(IRecord record)
    {
        if (GroupBy.Count == 0)
        {
            return AccessKey.Unbounded;
        }

        var source = new RecordRowValueSource(record);

        var values = new AccessValue[GroupBy.Count];

        for (var index = 0; index < GroupBy.Count; index++)
        {
            var column = GroupBy[index];

            if (FindField(record, column) is null)
            {
                throw new InvalidOperationException($"Row has no column '{column}' to group on");
            }

            values[index] = source.GetValue(-1, column).WithColumnName(column);
        }

        return new AccessKey(ImmutableCollectionsMarshal.AsImmutableArray(values));
    }

    private string RunningText()
        => string.Join(", ", _accumulators.Select(a => $"{a.Column.ToText()} = {AccessValueFormatter.ToText(a.Result)}"));

    private static string KeyText(AccessKey key)
        => key.IsUnbounded ? string.Empty : string.Join(", ", key.Values.Select(AccessValueFormatter.ToText));

    private static RecordField? FindField(IRecord? record, string column)
        => record?.Fields.FirstOrDefault(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase));
}
