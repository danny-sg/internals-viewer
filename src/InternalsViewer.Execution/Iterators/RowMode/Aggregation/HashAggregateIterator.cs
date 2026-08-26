using System.Numerics;
using System.Runtime.InteropServices;
using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.AccessPaths.Memory;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.RowMode.Aggregation;

/// <summary>
/// Hash Aggregate Operator
/// </summary>
/// <remarks>
/// Hash Aggregate is used for grouping and aggregation. It builds the data in a hash table that provides a fast way to locate keys based
/// on a calculated hash bucket and hash key.
///
/// The operator is used on un-sorted input. It is a blocking operator that will read all input rows and build the hash table before
/// returning any output rows.
///
/// Keys are hashed, then put in buckets, with each bucket containing chained entries with the hash, key, and aggregates for the GROUP BY
/// columns. The advantage of this approach is that it is very fast to locate a key in the hash table, so as it reads the un-sorted input
/// it can create or locate groups and update the aggregate values as the input provides rows.
/// </remarks>
public sealed class HashAggregateIterator(IIteratorFactory factory)
    : IteratorBase, IUnaryIterator, IHashTableIterator, IMemoryBufferIterator
{
    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    public IIterator? Input { get; private set; }

    public HashTable Table { get; private set; } = new(JoinHash.DefaultBucketBits);

    public BufferMemory Memory => Table.Memory;

    public long BuildRowEstimate { get; private set; }

    public IReadOnlyList<string> GroupBy { get; private set; } = [];

    public IReadOnlyList<AggregateColumn> Aggregates { get; private set; } = [];

    public long RowCount { get; private set; }

    public long InputRowCount { get; private set; }

    public long GroupCount => Table.RowCount;

    public long GroupRowCount => CurrentGroup?.RowCount ?? 0;

    public string CurrentKey { get; private set; } = string.Empty;

    public IReadOnlyList<AggregateValue> Running
        => CurrentGroup is { } group ? group.Values : [.. Aggregates.Select(Empty)];

    public IReadOnlyList<AggregateValue> GroupValues => AggregateGroupValues.Of(GroupBy, CurrentGroupKey);

    private AggregateGroupRecord? CurrentGroup { get; set; }

    private AccessKey CurrentGroupKey { get; set; }

    private int? PendingBucketBits { get; set; }

    private bool IsBuilt { get; set; }

    private bool IsPendingStart { get; set; }

    private int OutputBucket { get; set; }

    private int OutputEntry { get; set; }

    public void SetBucketCount(int bucketCount)
    {
        if (bucketCount < 2 || (bucketCount & (bucketCount - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount), bucketCount, "Bucket count must be a power of two.");
        }

        var bucketBits = BitOperations.Log2((uint)bucketCount);

        if (Input is null || IsComplete || IsBuilt)
        {
            Table.Resize(bucketBits);

            return;
        }

        PendingBucketBits = bucketBits;
    }

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var aggregate = definition.Expect<HashAggregateDefinition>();

        if (aggregate.GroupBy.Count == 0)
        {
            throw new ArgumentException("A hash aggregate needs at least one grouping column");
        }

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        GroupBy = aggregate.GroupBy;
        Aggregates = aggregate.Aggregates;

        BuildRowEstimate = aggregate.RowEstimate;

        Table = new HashTable(aggregate.BucketBits ?? JoinHash.BucketBitsFor(aggregate.RowEstimate));

        RowCount = 0;
        InputRowCount = 0;

        CurrentGroup = null;
        CurrentGroupKey = AccessKey.Unbounded;
        CurrentKey = string.Empty;

        PendingBucketBits = null;

        IsBuilt = false;
        IsPendingStart = true;

        OutputBucket = 0;
        OutputEntry = 0;

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

            var start = new AccessStep.AggregateStart(false)
            {
                Aggregates = string.Join(", ", Aggregates.Select(a => a.ToText())),
                GroupBy = string.Join(", ", GroupBy)
            };

            await EmitAsync(start, cancellationToken);
        }

        if (!IsBuilt)
        {
            await BuildAsync(cancellationToken);

            IsBuilt = true;
        }

        while (OutputBucket < Table.BucketCount)
        {
            var bucket = Table.Buckets[OutputBucket];

            if (OutputEntry >= bucket.Count)
            {
                OutputBucket++;
                OutputEntry = 0;

                continue;
            }

            var entry = bucket.Entries[OutputEntry++];

            if (entry.Record is not AggregateGroupRecord group)
            {
                continue;
            }

            RowCount++;

            CurrentGroup = group;
            CurrentGroupKey = entry.Key;
            CurrentKey = KeyText(entry.Key);

            CurrentRow = ProjectedRecord.Project(group, OutputList);

            var emit = new AccessStep.AggregateEmit(RowCount, CurrentKey)
            {
                EmittedRecord = group,
                Values = RunningText(group),
                GroupRows = group.RowCount
            };

            await EmitAsync(emit, cancellationToken);

            return CurrentRow;
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

    private async Task BuildAsync(CancellationToken cancellationToken)
    {
        while (await Input!.GetRowAsync(cancellationToken) is { } row)
        {
            ApplyPendingResize();

            var key = GetKey(row);

            var hash = JoinHash.Compute(key, key.Count);

            var (bucket, entry, group, isNew) = Find(hash, key, row);

            group.Add(new RecordRowValueSource(row), Context.EvaluationContext);

            InputRowCount++;

            CurrentGroup = group;
            CurrentGroupKey = key;
            CurrentKey = KeyText(key);

            var step = new AccessStep.HashAggregate(bucket, hash, entry)
            {
                Key = key,
                ChainLength = Table.Buckets[bucket].Count,
                BucketCount = Table.BucketCount,
                IsNewGroup = isNew,
                Number = InputRowCount,
                GroupRows = group.RowCount,
                Running = RunningText(group)
            };

            await EmitAsync(step, cancellationToken);
        }

        await Input.CloseAsync();
    }

    private (int Bucket, int Entry, AggregateGroupRecord Group, bool IsNew) Find(uint hash, AccessKey key, IRecord row)
    {
        var bucket = Table.GetBucket(hash);

        for (var index = 0; index < bucket.Count; index++)
        {
            var candidate = bucket.Entries[index];

            if (candidate.Hash == hash
                && candidate.Record is AggregateGroupRecord existing
                && candidate.Key.ComparePrefix(key, key.Count) == 0)
            {
                return (bucket.Index, index, existing, false);
            }
        }

        var group = new AggregateGroupRecord(GroupFields(row), Aggregates);

        var (added, entry) = Table.Add(hash, key, group, JoinHash.HasNull(key, key.Count));

        return (added, entry, group, true);
    }

    private List<RecordField> GroupFields(IRecord row)
    {
        var fields = new List<RecordField>(GroupBy.Count);

        foreach (var column in GroupBy)
        {
            if (FindField(row, column) is { } field)
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    private AccessKey GetKey(IRecord record)
    {
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

    private void ApplyPendingResize()
    {
        if (PendingBucketBits is { } bucketBits)
        {
            Table.Resize(bucketBits);

            PendingBucketBits = null;
        }
    }

    private static AggregateValue Empty(AggregateColumn column)
        => new(column.Column, column.ToText(), "NULL");

    private static string RunningText(AggregateGroupRecord group)
        => string.Join(", ", group.Accumulators.Select(a => $"{a.Column.ToText()} = {AccessValueFormatter.ToText(a.Result)}"));

    private static string KeyText(AccessKey key)
        => key.IsUnbounded ? string.Empty : string.Join(", ", key.Values.Select(AccessValueFormatter.ToText));

    private static RecordField? FindField(IRecord record, string column)
        => record.Fields.FirstOrDefault(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase));
}
