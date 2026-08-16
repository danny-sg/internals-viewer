using System.Runtime.InteropServices;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Memory;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Row;

/// <summary>
/// Sort Operator
/// </summary>
/// <remarks>
/// Sort is a blocking operator. It collects all rows into a sort buffer, then sorts and returns rows in order.
///
/// Sort has several options:
///
/// Is Distinct - Removes duplicates
///
///     Tracked at the ouput point - as rows are sorted the previous row values are referenced so only the first instance is output
///
/// Top Count - If the sort is for the top N rows
///
///     Uses a priority queue to retain only the top N rows, and then sorts those rows for output
/// </remarks>
public sealed class SortIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator, IRowBufferIterator, IMemoryBufferIterator
{
    private readonly List<JoinBufferRow> _table = [];

    private readonly List<AccessKey> _keys = [];

    private PriorityQueue<IRecord, AccessKey>? _queue;

    private long _rowBytes;

    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    public IIterator? Input { get; private set; }

    public long RowCount { get; private set; }

    public long CollectedCount { get; private set; }

    public IReadOnlyList<RowBuffer> Buffers => [new("Sort Table", 0, _table)];

    public BufferMemory Memory { get; private set; }

    private int OutputIndex { get; set; }

    private AccessKey? LastOutputKey { get; set; }

    private bool IsSorted { get; set; }

    private bool IsDistinct { get; set; }

    private long? TopCount { get; set; }

    private IReadOnlyList<SortKey> Keys { get; set; } = [];

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var sort = definition.Expect<SortDefinition>();

        if (sort.Keys.Count == 0)
        {
            throw new ArgumentException("A sort needs at least one key column");
        }

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        Keys = sort.Keys;
        IsDistinct = sort.IsDistinct;
        TopCount = sort.TopCount;

        _table.Clear();
        _keys.Clear();

        _queue = sort.TopCount is { } topCount
                 ? new PriorityQueue<IRecord, AccessKey>((int)topCount, Comparer<AccessKey>.Create((left, right) => Compare(right, left)))
                 : null;

        IsSorted = false;
        CollectedCount = 0;
        OutputIndex = 0;
        LastOutputKey = null;

        _rowBytes = 0;

        Memory = default;

        RowCount = 0;

        Input = factory.Create(sort.Source);

        await Input.OpenAsync(sort.Source, context, cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        if (!IsSorted)
        {
            await CollectAsync(cancellationToken);

            Sort();

            IsSorted = true;

            await EmitAsync(new AccessStep.Sorted(_table.Count), cancellationToken);
        }

        while (OutputIndex < _table.Count)
        {
            var index = OutputIndex++;

            var key = _keys[index];

            if (IsDistinct && LastOutputKey is { } last && Compare(key, last) == 0)
            {
                _table[index] = _table[index] with { State = JoinRowState.Finished };

                await EmitAsync(new AccessStep.SortDuplicate(index + 1), cancellationToken);

                continue;
            }

            LastOutputKey = key;

            RowCount++;

            var record = _table[index].Record;

            _table[index] = _table[index] with { State = JoinRowState.Matched };

            await EmitAsync(new AccessStep.SortRow(RowCount) { EmittedRecord = record }, cancellationToken);

            CurrentRow = ProjectedRecord.Project(record, OutputList);

            return CurrentRow;
        }

        CurrentRow = null;

        await EmitAsync(new AccessStep.Stopped(AccessPaths.Results.StopReason.PageExhausted), cancellationToken);

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

    /// <summary>
    /// Collect rows from input and build the sort table/priority queue
    /// </summary>
    private async Task CollectAsync(CancellationToken cancellationToken)
    {
        while (await Input!.GetRowAsync(cancellationToken) is { } row)
        {
            CollectedCount++;

            var key = GetKey(row);

            var isRetained = true;

            if (_queue is { } queue && TopCount is { } topCount)
            {
                if (queue.Count < topCount)
                {
                    queue.Enqueue(row, key);

                    _rowBytes += RowMemory.SizeOf(row);
                }
                else
                {
                    var dropped = queue.EnqueueDequeue(row, key);

                    isRetained = !ReferenceEquals(dropped, row);

                    if (isRetained)
                    {
                        _rowBytes += RowMemory.SizeOf(row) - RowMemory.SizeOf(dropped);
                    }
                }
            }
            else
            {
                _table.Add(new JoinBufferRow(row, JoinRowState.Pending));

                _keys.Add(key);

                _rowBytes += RowMemory.SizeOf(row);
            }

            if (_queue is not null)
            {
                RebuildFromQueue();
            }

            UpdateMemory();

            await EmitAsync(new AccessStep.SortCollect(CollectedCount) { IsRetained = isRetained }, cancellationToken);
        }

        await Input.CloseAsync();
    }

    /// <summary>
    /// Totals what the sort is holding, which a top N sort tracks through what it dropped rather than by recounting the rows it kept
    /// </summary>
    private void UpdateMemory()
    {
        Memory = RowMemory.ForSort(_rowBytes, _table.Count);
    }

    private void RebuildFromQueue()
    {
        _table.Clear();
        _keys.Clear();

        foreach (var (record, key) in _queue!.UnorderedItems)
        {
            _table.Add(new JoinBufferRow(record, JoinRowState.Pending));
            _keys.Add(key);
        }
    }

    private void Sort()
    {
        var order = new int[_table.Count];

        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        Array.Sort(order, (left, right) => Compare(_keys[left], _keys[right]));

        var sortedTable = new JoinBufferRow[order.Length];

        var sortedKeys = new AccessKey[order.Length];

        for (var index = 0; index < order.Length; index++)
        {
            sortedTable[index] = _table[order[index]];
            sortedKeys[index] = _keys[order[index]];
        }

        _table.Clear();
        _table.AddRange(sortedTable);

        _keys.Clear();
        _keys.AddRange(sortedKeys);
    }

    private int Compare(AccessKey left, AccessKey right)
    {
        for (var index = 0; index < Keys.Count; index++)
        {
            var comparison = AccessValueComparer.Compare(left[index], right[index]);

            if (comparison != 0)
            {
                return Keys[index].Descending ? -comparison : comparison;
            }
        }

        return 0;
    }

    private AccessKey GetKey(IRecord record)
    {
        var source = new RecordRowValueSource(record);

        var values = new AccessValue[Keys.Count];

        for (var index = 0; index < Keys.Count; index++)
        {
            var column = Keys[index].Column;

            if (!record.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Row has no column '{column}' to build the sort key");
            }

            values[index] = source.GetValue(-1, column).WithColumnName(column);
        }

        return new AccessKey(ImmutableCollectionsMarshal.AsImmutableArray(values));
    }
}
