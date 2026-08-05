using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Row;

public sealed class SortIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator, IRowBufferIterator
{
    private readonly List<JoinBufferRow> _table = [];

    private readonly List<AccessKey> _keys = [];

    private PriorityQueue<IRecord, AccessKey>? _queue;

    private bool _isSorted;

    private long _collected;

    private int _outputIndex;

    private AccessKey? _lastOutputKey;

    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    public IIterator? Input { get; private set; }

    public long RowCount { get; private set; }

    public long CollectedCount => _collected;

    public IReadOnlyList<RowBuffer> Buffers => [new RowBuffer("Sort Table", 0, _table)];

    private bool IsDistinct { get; set; }

    private long? TopCount { get; set; }

    private IReadOnlyList<SortKey> Keys { get; set; } = [];

    public override async Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
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

        await PrepareAsync(context, definition, cancellationToken);

        Keys = sort.Keys;
        IsDistinct = sort.IsDistinct;
        TopCount = sort.TopCount;

        _table.Clear();
        _keys.Clear();

        _queue = sort.TopCount is { } topCount
            ? new PriorityQueue<IRecord, AccessKey>((int)topCount, Comparer<AccessKey>.Create((left, right) => Compare(right, left)))
            : null;

        _isSorted = false;
        _collected = 0;
        _outputIndex = 0;
        _lastOutputKey = null;
        RowCount = 0;

        Input = factory.Create(sort.Source);

        await Input.OpenAsync(context, sort.Source, cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        if (!_isSorted)
        {
            await CollectAsync(cancellationToken);

            Sort();

            _isSorted = true;

            await EmitAsync(new AccessStep.Sorted(_table.Count), cancellationToken);
        }

        while (_outputIndex < _table.Count)
        {
            var index = _outputIndex++;

            var key = _keys[index];

            if (IsDistinct && _lastOutputKey is { } last && Compare(key, last) == 0)
            {
                _table[index] = _table[index] with { State = JoinRowState.Finished };

                await EmitAsync(new AccessStep.SortDuplicate(index + 1), cancellationToken);

                continue;
            }

            _lastOutputKey = key;

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

    private async Task CollectAsync(CancellationToken cancellationToken)
    {
        while (await Input!.GetRowAsync(cancellationToken) is { } row)
        {
            _collected++;

            var key = GetKey(row);

            var isRetained = true;

            if (_queue is { } queue && TopCount is { } topCount)
            {
                if (queue.Count < topCount)
                {
                    queue.Enqueue(row, key);
                }
                else
                {
                    isRetained = !ReferenceEquals(queue.EnqueueDequeue(row, key), row);
                }

                RebuildFromQueue();
            }
            else
            {
                _table.Add(new JoinBufferRow(row, JoinRowState.Pending));
                _keys.Add(key);
            }

            await EmitAsync(new AccessStep.SortCollect(_collected) { IsRetained = isRetained }, cancellationToken);
        }

        await Input.CloseAsync();
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

        return new AccessKey([.. values]);
    }
}
