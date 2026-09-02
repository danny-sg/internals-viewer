using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.AccessPaths.Memory;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Common;
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

    public HashTable Table => Builder.Table;

    public BufferMemory Memory => Table.Memory;

    public long BuildRowEstimate { get; private set; }

    public IReadOnlyList<string> GroupBy => Builder.GroupBy;

    public IReadOnlyList<AggregateColumn> Aggregates => Builder.Aggregates;

    public long RowCount { get; private set; }

    public long InputRowCount => Builder.InputRowCount;

    public long GroupCount => Table.RowCount;

    public long GroupRowCount => CurrentGroup?.RowCount ?? 0;

    public string CurrentKey { get; private set; } = string.Empty;

    public IReadOnlyList<AggregateValue> Running
        => CurrentGroup is { } group ? group.Values : [.. Aggregates.Select(Empty)];

    public IReadOnlyList<AggregateValue> GroupValues => AggregateGroupValues.Of(GroupBy, CurrentGroupKey);

    private HashAggregateBuilder Builder { get; set; } = new([], [], JoinHash.DefaultBucketBits);

    private AggregateGroupRecord? CurrentGroup { get; set; }

    private AccessKey CurrentGroupKey { get; set; }

    private bool IsBuilt { get; set; }

    private bool IsPendingStart { get; set; }

    private int OutputBucket { get; set; }

    private int OutputEntry { get; set; }

    public void SetBucketCount(int bucketCount)
        => Builder.Resize(HashAggregateBuilder.BucketBitsOf(bucketCount), Input is null || IsComplete || IsBuilt);

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var aggregate = definition.Expect<HashAggregateDefinition>();

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        BuildRowEstimate = aggregate.RowEstimate;

        Builder = new HashAggregateBuilder(aggregate.GroupBy,
                                           aggregate.Aggregates,
                                           aggregate.BucketBits ?? JoinHash.BucketBitsFor(aggregate.RowEstimate));

        RowCount = 0;

        CurrentGroup = null;
        CurrentGroupKey = AccessKey.Unbounded;
        CurrentKey = string.Empty;

        IsBuilt = false;
        IsPendingStart = true;

        OutputBucket = 0;
        OutputEntry = 0;

        Input = factory.Create(aggregate.Source);

        await Input.OpenAsync(aggregate.Source, context, cancellationToken);
    }

    public override async ValueTask<IRecord?> GetRowAsync(CancellationToken cancellationToken)
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
                Values = HashAggregateBuilder.RunningText(group),
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
            var hit = Builder.Accumulate(row, Context.EvaluationContext);

            CurrentGroup = hit.Group;
            CurrentGroupKey = hit.Key;
            CurrentKey = KeyText(hit.Key);

            var step = new AccessStep.HashAggregate(hit.Bucket, hit.Hash, hit.Entry)
            {
                Key = hit.Key,
                ChainLength = Table.Buckets[hit.Bucket].Count,
                BucketCount = Table.BucketCount,
                IsNewGroup = hit.IsNew,
                Number = InputRowCount,
                GroupRows = hit.Group.RowCount,
                Running = HashAggregateBuilder.RunningText(hit.Group)
            };

            await EmitAsync(step, cancellationToken);
        }

        await Input.CloseAsync();
    }

    private static AggregateValue Empty(AggregateColumn column)
        => new(column.Column, column.ToText(), "NULL");

    private static string KeyText(AccessKey key)
        => key.IsUnbounded ? string.Empty : string.Join(", ", key.Values.Select(AccessValueFormatter.ToText));

    private static RecordField? FindField(IRecord record, string column)
        => record.Fields.FirstOrDefault(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase));
}
