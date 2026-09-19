using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Execution.Iterators.Common;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Services;

namespace InternalsViewer.Execution.Iterators.BatchMode.DataAccess;

/// <summary>
/// Reads compressed row groups of a columnstore index as batches
/// </summary>
/// <remarks>
/// This iterator replicates a Columnstore Index Scan operator, but internally it's not implemented like for like.
///
/// See - <see href="https://medium.com/internals-viewer/columnstore-index-scan-internals-2c164aef8750"/>
/// </remarks>
public sealed class ColumnstoreScanIterator(ColumnstoreService columnstoreService) 
    : IBatchIterator, IAggregatePushdownTarget
{
    public int NodeId { get; private set; }

    public bool IsComplete { get; private set; }

    public StopReason? StopReason { get; private set; }

    public ExecutionBatch? CurrentBatch => Batch;

    public IReadOnlyList<BatchVector> OutputVectors => OwnVectors;

    public IBatchIterator? Input => null;

    public long BatchNumber { get; private set; }

    public bool IsAggregatePushdown => AggregatePushdown.IsActive;

    public long LocallyAggregatedRows => AggregatePushdown.LocallyAggregatedRows;

    private ColumnstoreScanAggregatePushdown AggregatePushdown { get; } = new();

    private IteratorContext Context { get; set; } = null!;

    private ColumnstoreScanDefinition Definition { get; set; } = null!;

    private List<ScanColumn> Columns { get; set; } = [];

    private RowGroupReader? Reader { get; set; }

    private ColumnstoreScanRowGroupCursor Cursor { get; set; } = null!;

    private int RowOrdinal { get; set; }

    private DeletedRows DeletedRows { get; set; } = DeletedRows.None;

    private List<RowGroup> RowGroups { get; set; } = [];

    private List<int> ColumnIds { get; set; } = [];

    private int BatchRows { get; set; } = BatchSize.MaxRowCount;

    private bool[] RowMask { get; } = new bool[BatchSize.MaxRowCount];

    private bool HasCompressedFilter => Columns.Exists(c => c.Filter is not null);

    private BatchRowValueSource Values { get; } = new();

    private ExecutionBatch? Batch { get; set; }

    private List<BatchVector> OwnVectors { get; set; } = [];

    private List<BatchVector> BoundVectors { get; } = [];

    private int RowGroupRowCount { get; set; }

    private AccessPredicate? Predicate { get; set; }

    private string PredicateColumnNames { get; set; } = string.Empty;

    private long VectorNumber { get; set; }

    public async Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken)
    {
        Definition = definition.Expect<ColumnstoreScanDefinition>();

        Context = context;

        NodeId = definition.NodeId;

        Reset();

        await EmitAsync(new AccessStep.Open(), cancellationToken);

        if (Definition.AllocationUnit is { } allocationUnit)
        {
            var index = await columnstoreService.GetIndex(allocationUnit, context.Database, cancellationToken);

            ColumnIds = ResolveColumns(index);

            (Batch, OwnVectors) = ColumnstoreScanBatchFactory.Create(ResolveColumnNames(index), Definition.PipelineColumnNames);

            BatchRows = Batch.Capacity;

            DeletedRows = await columnstoreService.GetDeletedRows(context.Database, index, cancellationToken);

            RowGroups = [.. index.CompressedRowGroups.OrderByDescending(r => r.RowGroupId)];
        }

        Cursor = new ColumnstoreScanRowGroupCursor(columnstoreService, Definition, Context, NodeId, ColumnIds, RowGroups);
    }

    public async ValueTask<ExecutionBatch?> GetNextBatchAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Reader is null && !await MoveToNextRowGroupAsync(cancellationToken))
            {
                IsComplete = true;

                StopReason = AccessPaths.Results.StopReason.RowGroupsExhausted;

                await EmitAsync(new AccessStep.Stopped(StopReason.Value), cancellationToken);

                return null;
            }

            var remaining = RowGroupRowCount - RowOrdinal;

            if (remaining <= 0)
            {
                Reader = null;

                continue;
            }

            var bucketSize = ColumnstoreScanRowGroupCursor.GetRowBucketSize(Columns, RowOrdinal, Math.Min(BatchRows, remaining));

            var batch = await FillBatchAsync(bucketSize, cancellationToken);

            if (batch is null)
            {
                continue;
            }

            return batch;
        }
    }

    public async Task CloseAsync()
    {
        Reader = null;

        Columns = [];

        Batch = null;

        IsComplete = true;

        await EmitAsync(new AccessStep.Close(), CancellationToken.None);
    }

    public void SetPushdownSink(HashAggregateBuilder builder, EvaluationContext context) 
        => AggregatePushdown.SetSink(builder, context);

    private ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken)
        => Context.Steps.EmitAsync(step with { NodeId = NodeId }, cancellationToken);

    private async Task<ExecutionBatch?> FillBatchAsync(int size, CancellationToken cancellationToken)
    {
        var batch = Batch!;

        batch.Reset(size);

        var rowGroupId = batch.RowGroupId;

        var filterRleEntryCount = 0;

        var filterOperationCount = 0;

        var pureColumns = 0;

        var impureColumns = 0;

        RowMask.AsSpan(0, size).Fill(true);

        var deleted = ColumnstoreScanFilter.ApplyDeletes(RowMask.AsSpan(0, size), DeletedRows, rowGroupId, RowOrdinal);

        ColumnstoreScanFilter.ApplyCompressed(RowMask.AsSpan(0, size), 
                                              Columns, 
                                              RowOrdinal, 
                                              ref filterRleEntryCount, 
                                              ref filterOperationCount);

        var materialised = 0;

        if (RowMask.AsSpan(0, size).IndexOf(true) >= 0)
        {
            materialised = ColumnstoreScanProjector.Fill(Columns, BoundVectors, RowOrdinal, RowMask.AsSpan(0, size), batch.DeepDataContext);

            await ApplyPredicateAsync(batch, size, cancellationToken);

            pureColumns = BoundVectors.Count(v => v.IsPure);
            impureColumns = BoundVectors.Count - pureColumns;
        }

        ColumnstoreScanProjector.Compact(batch, BoundVectors, RowMask.AsSpan(0, size));

        if (deleted > 0)
        {
            await EmitAsync(new AccessStep.DeleteBitmapApplied(rowGroupId, deleted), cancellationToken);
        }

        RowOrdinal += size;

        if (batch.SelectionVector.RowCount == 0)
        {
            await EmitAsync(new AccessStep.BatchSkipped(rowGroupId, RowOrdinal - size, size)
                            {
                                FilterRleEntries = filterRleEntryCount,
                                FilterOperations = filterOperationCount,
                                HasCompressedFilter = HasCompressedFilter,
                                HasPredicate = Predicate is not null || Definition.IsGenericFilterUsed
                            },
                            cancellationToken);

            return null;
        }

        if (AggregatePushdown.IsActive)
        {
            var (rows, totalGroups, newGroups, runFolded) = AggregatePushdown.Accumulate(batch);

            await EmitAsync(new AccessStep.AggregatePushdown(rowGroupId,
                                                             RowOrdinal - size,
                                                             rows,
                                                             totalGroups,
                                                             newGroups,
                                                             runFolded),
                            cancellationToken);

            return null;
        }

        BatchNumber++;

        await EmitAsync(new AccessStep.BatchProduced(BatchNumber,
                                                     rowGroupId,
                                                     RowOrdinal - size,
                                                     size,
                                                     batch.SelectionVector.RowCount)
                        {
                            FilterRleEntries = filterRleEntryCount,
                            FilterOperations = filterOperationCount,
                            Materialised = materialised,
                            HasCompressedFilter = HasCompressedFilter,
                            HasPredicate = Predicate is not null || Definition.IsGenericFilterUsed,
                            PureColumns = pureColumns,
                            ImpureColumns = impureColumns
                        },
                        cancellationToken);

        return batch;
    }

    private async Task ApplyPredicateAsync(ExecutionBatch batch, int size, CancellationToken cancellationToken)
    {
        if (Predicate is null)
        {
            return;
        }

        var (evaluated, matches) = ColumnstoreScanFilter.EvaluateResidual(batch,
                                                                          RowMask.AsSpan(0, size),
                                                                          Predicate!,
                                                                          Context.EvaluationContext,
                                                                          Values);

        VectorNumber++;

        await EmitAsync(new AccessStep.FilterVector(VectorNumber, batch.RowGroupId, PredicateColumnNames, evaluated, matches),
                        cancellationToken);
    }

    /// <summary>
    /// Takes the next rowgroup the cursor hands back, binds the batch to it, and emits its opened and filter steps
    /// </summary>
    /// <remarks>
    /// The cursor advances past eliminated rowgroups and emits their elimination steps. This sets up the surviving rowgroup for reading and
    /// returns false only when the cursor is exhausted.
    /// </remarks>
    private async Task<bool> MoveToNextRowGroupAsync(CancellationToken cancellationToken)
    {
        if (await Cursor.NextAsync(cancellationToken) is not { } live)
        {
            return false;
        }

        Reader = live.Reader;

        Columns = live.Columns;

        Predicate = ResolvePredicate(Columns);

        PredicateColumnNames = Predicate is null
                               ? string.Empty
                               : string.Join(", ", PredicateColumns.Referenced(Predicate).Distinct());

        BindBatch(live.RowGroup);

        RowGroupRowCount = ColumnIds.Count > 0 ? live.Reader.RowCount : live.RowGroup.TotalRows;

        RowOrdinal = 0;

        await EmitAsync(new AccessStep.RowGroupOpened(live.RowGroup.RowGroupId, Columns.Count, BatchRows), cancellationToken);

        if (Columns.Where(c => c.Filter is not null).Select(c => c.Column.Name).ToList() is { Count: > 0 } filtered)
        {
            await EmitAsync(new AccessStep.CompressedDataFilter(live.RowGroup.RowGroupId, string.Join(", ", filtered), true),
                            cancellationToken);

            await EmitFilterBitmapsAsync(live.RowGroup, Columns, cancellationToken);
        }
        else if (Predicate is not null)
        {
            await EmitAsync(new AccessStep.CompressedDataFilter(live.RowGroup.RowGroupId, PredicateColumnNames, false),
                            cancellationToken);
        }

        return true;
    }

    private async Task EmitFilterBitmapsAsync(RowGroup rowGroup, List<ScanColumn> columns, CancellationToken cancellationToken)
    {
        foreach (var column in columns)
        {
            if (column.Filter is not { Category: CompressedFilterCategory.RawBitmap or CompressedFilterCategory.Equality } filter
                || filter.QualifyingDataIds is not { } qualifying)
            {
                continue;
            }

            var dictionaryIds = column.Reader.DictionaryDataIds.OrderBy(id => id).ToArray();

            if (dictionaryIds.Length == 0)
            {
                continue;
            }

            var matched = qualifying as HashSet<long> ?? [.. qualifying];

            var flags = new bool[dictionaryIds.Length];

            var values = new string[dictionaryIds.Length];

            var qualifyingCount = 0;

            for (var i = 0; i < dictionaryIds.Length; i++)
            {
                values[i] = column.Reader.GetValueForDataId(dictionaryIds[i])?.ToString() ?? string.Empty;

                if (!matched.Contains(dictionaryIds[i]))
                {
                    continue;
                }

                flags[i] = true;

                qualifyingCount++;
            }

            await EmitAsync(new AccessStep.CompressedDataFilterBitmap(rowGroup.RowGroupId,
                                                                      column.Reader.Segment.Column?.ColumnStoreColumnId ?? -1,
                                                                      column.Column.Name,
                                                                      filter.Category,
                                                                      dictionaryIds,
                                                                      flags,
                                                                      values,
                                                                      qualifyingCount),
                            cancellationToken);
        }
    }

    /// <summary>
    /// Re-binds the Batch to the rowgroup
    /// </summary>
    private void BindBatch(RowGroup rowGroup)
    {
        if (Batch is not { } batch)
        {
            return;
        }

        batch.RowGroupId = rowGroup.RowGroupId;

        BoundVectors.Clear();

        foreach (var column in Columns)
        {
            var vector = batch.FindVector(column.Column.Name);

            if (vector is null)
            {
                vector = new BatchVector(column.Column, BatchRows);

                batch.AddVector(vector);

                OwnVectors.Add(vector);
            }

            vector.Column = column.Column;

            vector.Source = column.Reader;

            BoundVectors.Add(vector);
        }
    }

    private AccessPredicate? ResolvePredicate(List<ScanColumn> columns)
    {
        if (Definition.Residual is not { } residual || residual is AccessPredicate.True or AccessPredicate.NoTranslation)
        {
            return null;
        }

        if (!CompressedDataFilter.IsPlainConjunction(residual))
        {
            return residual;
        }

        var claimed = columns.SelectMany(c => c.Filter?.Claimed ?? []).ToList();

        var unclaimed = CompressedDataFilter.Conjunctions(residual)
                                            .Where(c => !claimed.Contains(c))
                                            .Cast<AccessPredicate>()
                                            .ToList();

        return unclaimed.Count switch
        {
            0 => null,
            1 => unclaimed[0],
            _ => new AccessPredicate.And([.. unclaimed])
        };
    }

    private List<string> ResolveColumnNames(ColumnStoreIndex index)
    {
        var byId = index.Columns.ToDictionary(c => c.ColumnStoreColumnId, c => c.Name);

        return [.. ColumnIds.Where(byId.ContainsKey).Select(id => byId[id])];
    }

    private List<int> ResolveColumns(ColumnStoreIndex index)
    {
        if (Definition.ColumnNames.Count == 0)
        {
            return [];
        }

        var byName = index.Columns.ToDictionary(c => c.Name, c => c.ColumnStoreColumnId, StringComparer.OrdinalIgnoreCase);

        return [.. Definition.ColumnNames.Where(byName.ContainsKey).Select(n => byName[n])];
    }

    private void Reset()
    {
        RowOrdinal = 0;

        BatchNumber = 0;

        Reader = null;

        Columns = [];

        Batch = null;

        Predicate = null;

        PredicateColumnNames = string.Empty;

        VectorNumber = 0;

        IsComplete = false;

        StopReason = null;

        RowGroups = [];

        ColumnIds = [];

        DeletedRows = DeletedRows.None;
    }
}
