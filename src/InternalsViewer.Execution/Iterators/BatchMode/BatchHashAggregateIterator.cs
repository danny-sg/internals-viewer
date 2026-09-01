using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.AccessPaths.Memory;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Common;
using InternalsViewer.Execution.Records;

namespace InternalsViewer.Execution.Iterators.BatchMode;

/// <summary>
/// Hash Aggregate reading and producing batches
/// </summary>
public sealed class BatchHashAggregateIterator(IIteratorFactory factory) : IBatchIterator, IHashTableSource
{
    public int NodeId { get; private set; }

    public bool IsComplete { get; private set; }

    public StopReason? StopReason { get; private set; }

    public ExecutionBatch? CurrentBatch => Batch;

    public IBatchIterator? Input { get; private set; }

    public IReadOnlyList<BatchVector> OutputVectors => Batch?.Vectors ?? [];

    public long BatchNumber => OutputBatchCount;

    public HashTable Table => Builder.Table;

    public IHashTableSource LocalHashTable => LocalSource;

    public BufferMemory Memory
    {
        get
        {
            var rows = Builder.Table.RowCount + LocalBuilder.Table.RowCount;

            var rowBytes = Builder.Table.RowBytes + LocalBuilder.Table.RowBytes + DeepDataBytes;

            return new BufferMemory(rowBytes, (rows * RowMemory.WorkspaceRowBytes) + RowMemory.HashFloorBytes);
        }
    }

    public long DeepDataBytes => Batch?.DeepDataContext.ByteCount ?? 0;

    public long BuildRowEstimate { get; private set; }

    public long InputRowCount => Builder.InputRowCount + LocalBuilder.InputRowCount;

    public long GroupCount => Builder.GroupCount;

    public long BatchCount { get; private set; }

    private long OutputBatchCount { get; set; }

    private HashAggregateBuilder Builder { get; set; } = new([], [], JoinHash.DefaultBucketBits);

    private HashAggregateBuilder LocalBuilder { get; set; } = new([], [], JoinHash.DefaultBucketBits);

    private HashAggregateLocalSource LocalSource { get; set; } = new(new([], [], JoinHash.DefaultBucketBits), 0);

    private int LocalRowGroupId { get; set; } = -1;

    private bool IsPushdown { get; set; }

    private IteratorContext Context { get; set; } = null!;

    private ExecutionBatch? Batch { get; set; }

    private IReadOnlyList<OutputColumn> OutputList { get; set; } = [];

    private bool IsBuilt { get; set; }

    private int OutputBucket { get; set; }

    private int OutputEntry { get; set; }

    public void SetBucketCount(int bucketCount)
        => Builder.Resize(HashAggregateBuilder.BucketBitsOf(bucketCount), Input is null || IsComplete || IsBuilt);

    public async Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken)
    {
        var aggregate = definition.Expect<BatchHashAggregateDefinition>();

        Context = context;

        NodeId = definition.NodeId;

        OutputList = aggregate.OutputList;

        BuildRowEstimate = aggregate.RowEstimate;

        Builder = new HashAggregateBuilder(aggregate.GroupBy,
                                           aggregate.Aggregates,
                                           aggregate.BucketBits ?? JoinHash.BucketBitsFor(aggregate.RowEstimate));

        LocalBuilder = new HashAggregateBuilder(aggregate.GroupBy,
                                                aggregate.Aggregates,
                                                aggregate.BucketBits ?? JoinHash.BucketBitsFor(aggregate.RowEstimate));

        LocalSource = new HashAggregateLocalSource(LocalBuilder, aggregate.RowEstimate);

        LocalRowGroupId = -1;

        Batch = null;

        IsPushdown = false;

        BatchCount = 0;

        OutputBatchCount = 0;

        OutputBucket = 0;

        OutputEntry = 0;

        IsBuilt = false;

        IsComplete = false;

        StopReason = null;

        await EmitAsync(new AccessStep.AggregateStart(aggregate.GroupBy.Count == 0)
        {
            Aggregates = string.Join(", ", aggregate.Aggregates.Select(a => a.ToText())),
            GroupBy = string.Join(", ", aggregate.GroupBy)
        },
        cancellationToken);

        Input = factory.CreateBatch(aggregate.Source);

        if (aggregate.Source is ColumnstoreScanDefinition { IsAggregatePushdown: true }
            && Input is IAggregatePushdownTarget target)
        {
            target.SetPushdownSink(LocalBuilder, context.EvaluationContext);

            IsPushdown = true;
        }

        await Input.OpenAsync(aggregate.Source, context, cancellationToken);
    }

    public async Task<ExecutionBatch?> GetNextBatchAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        if (!IsBuilt)
        {
            await BuildAsync(cancellationToken);

            IsBuilt = true;
        }

        return await EmitBatchAsync(cancellationToken);
    }

    public async Task CloseAsync()
    {
        if (Input is not null)
        {
            await Input.CloseAsync();
        }

        IsComplete = true;

        await EmitAsync(new AccessStep.Close(), CancellationToken.None);
    }

    private async Task BuildAsync(CancellationToken cancellationToken)
    {
        while (await Input!.GetNextBatchAsync(cancellationToken) is { } batch)
        {
            BatchCount++;

            var selection = batch.SelectionVector;

            if (LocalRowGroupId >= 0 && batch.RowGroupId != LocalRowGroupId)
            {
                await MergeLocalAsync(cancellationToken);
            }

            LocalRowGroupId = batch.RowGroupId;

            var groupsBefore = LocalBuilder.GroupCount;

            var lastKey = string.Empty;

            var running = string.Empty;

            for (var index = 0; index < selection.RowCount; index++)
            {
                var row = BatchRecordBuilder.Build(batch, selection[index]);

                var hit = LocalBuilder.Accumulate(row,
                                                  Context.EvaluationContext,
                                                  BatchRecordBuilder.BuildLocalKey(batch, selection[index], LocalBuilder.GroupBy),
                                                  skipKeyFields: 1);

                lastKey = KeyText(hit.Key);

                running = HashAggregateBuilder.RunningText(hit.Group);
            }

            await EmitAsync(new AccessStep.HashAggregateBatch(BatchCount, selection.RowCount)
            {
                InputRowCount = Builder.InputRowCount + LocalBuilder.InputRowCount,
                Groups = LocalBuilder.GroupCount,
                NewGroups = LocalBuilder.GroupCount - groupsBefore,
                BucketCount = LocalBuilder.Table.BucketCount,
                Fill = TableFill(LocalBuilder),
                LastKey = lastKey,
                Running = running
            },
            cancellationToken);
        }

        await MergeLocalAsync(cancellationToken);
    }

    private async Task MergeLocalAsync(CancellationToken cancellationToken)
    {
        if (LocalBuilder.GroupCount == 0)
        {
            return;
        }

        var localGroups = LocalBuilder.GroupCount;

        var globalBefore = Builder.GroupCount;

        var materialised = 0L;

        foreach (var bucket in LocalBuilder.Table.Buckets)
        {
            for (var index = 0; index < bucket.Count; index++)
            {
                if (bucket.Entries[index].Record is not AggregateGroupRecord group)
                {
                    continue;
                }

                Builder.Merge(group);

                materialised++;
            }
        }

        await EmitAsync(new AccessStep.AggregateLocalMerge(LocalRowGroupId,
                                                           localGroups,
                                                           globalBefore,
                                                           Builder.GroupCount,
                                                           materialised),
                        cancellationToken);

        LocalBuilder.Clear();
    }

    private async Task<ExecutionBatch?> EmitBatchAsync(CancellationToken cancellationToken)
    {
        var groups = new List<AggregateGroupRecord>();

        while (OutputBucket < Table.BucketCount && groups.Count < BatchSize.MaxRowCount)
        {
            var bucket = Table.Buckets[OutputBucket];

            if (OutputEntry >= bucket.Count)
            {
                OutputBucket++;

                OutputEntry = 0;

                continue;
            }

            if (bucket.Entries[OutputEntry++].Record is AggregateGroupRecord group)
            {
                groups.Add(group);
            }
        }

        if (groups.Count == 0)
        {
            IsComplete = true;

            StopReason = AccessPaths.Results.StopReason.RowGroupsExhausted;

            await EmitAsync(new AccessStep.Stopped(StopReason.Value), cancellationToken);

            return null;
        }

        Batch = BatchPacker.Pack(groups.Select(g => ProjectedRecord.Project(g, OutputList)), Batch);

        OutputBatchCount++;

        await EmitAsync(new AccessStep.BatchProduced(OutputBatchCount, 0, 0, Batch.RowCount, Batch.SelectionVector.RowCount),
                        cancellationToken);

        return Batch;
    }

    private static int[] TableFill(HashAggregateBuilder builder)
    {
        var fill = new int[builder.Table.BucketCount];

        for (var index = 0; index < fill.Length; index++)
        {
            fill[index] = builder.Table.Buckets[index].Count;
        }

        return fill;
    }

    private static string KeyText(AccessKey key)
        => key.Count == 0 ? "()" : string.Join(", ", Enumerable.Range(0, key.Count).Select(i => key[i].ToString()));

    private ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken)
        => Context.Steps.EmitAsync(step with { NodeId = NodeId }, cancellationToken);
}
