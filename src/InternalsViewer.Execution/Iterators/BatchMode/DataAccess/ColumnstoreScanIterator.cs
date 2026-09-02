using System.Data;
using System.Data.SqlTypes;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Elimination;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Normalization;
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
/// Columnstore scans do the following in each stage:
///
/// OpenAsync()
/// 
///   - Create a new batch (scan should be at the bottom of the operator tree)
/// 
///   - Source deleted rows from the Delete Bitmap
/// 
///   - Row Group elimination to produce a set of non-eliminated row groups
///     - Partition elimination eliminates partitions using the boundaries (not yet implemented)
///     - Segment elimination eliminates based on the max and min values held in metadata - <see cref="SegmentEliminator"/>. If the segment
///       can't satisfy the required predicate the row group won't either so it's eliminated.
/// 
/// GetNextBatchAsync()
///
///    - Selected Row Groups will be iterated through
///    - The scan fills the batch vectors for this batch
/// </remarks>
public sealed class ColumnstoreScanIterator(ColumnstoreService columnstoreService) : IBatchIterator, IAggregatePushdownTarget
{
    public int NodeId { get; private set; }

    public bool IsComplete { get; private set; }

    public StopReason? StopReason { get; private set; }

    public ExecutionBatch? CurrentBatch => Batch;

    public IReadOnlyList<BatchVector> OutputVectors => OwnVectors;

    public IBatchIterator? Input => null;

    public long BatchNumber { get; private set; }

    public bool IsAggregatePushdown => PushdownSink is not null;

    public long LocallyAggregatedRows { get; private set; }

    private HashAggregateBuilder? PushdownSink { get; set; }

    private EvaluationContext? PushdownContext { get; set; }

    private IteratorContext Context { get; set; } = null!;

    private ColumnstoreScanDefinition Definition { get; set; } = null!;

    private List<ScanColumn> Columns { get; set; } = [];

    private RowGroupReader? Reader { get; set; }

    private int RowGroupIndex { get; set; }

    private int RowOrdinal { get; set; }

    private DeletedRows DeletedRows { get; set; } = DeletedRows.None;

    private List<RowGroup> RowGroups { get; set; } = [];

    private List<int> ColumnIds { get; set; } = [];

    private int BatchRows { get; set; } = BatchSize.MaxRowCount;

    private PartitionEliminator Partitions { get; set; } = new(null);

    private SegmentEliminator Segments { get; set; } = new(null);

    private HashSet<long> SkippedPartitions { get; } = [];

    private HashSet<(long HobtId, int ColumnId, int DictionaryId)> OpenDictionaries { get; } = [];

    private bool[] RowMask { get; } = new bool[BatchSize.MaxRowCount];

    private bool HasCompressedFilter => Columns.Exists(c => c.Filter is not null);

    private BatchRowValueSource Values { get; } = new();

    private ExecutionBatch? Batch { get; set; }

    private List<BatchVector> OwnVectors { get; } = [];

    private List<BatchVector> BoundVectors { get; } = [];

    private int RowGroupRowCount { get; set; }

    private AccessPredicate? Predicate { get; set; }

    private string PredicateColumnNames { get; set; } = string.Empty;

    private long VectorNumber { get; set; }

    public void SetPushdownSink(HashAggregateBuilder builder, EvaluationContext context)
    {
        PushdownSink = builder;

        PushdownContext = context;
    }

    public async Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken)
    {
        Definition = definition.Expect<ColumnstoreScanDefinition>();

        Context = context;

        NodeId = definition.NodeId;

        Reset();

        Partitions = new PartitionEliminator(Definition.Residual);

        Segments = new SegmentEliminator(Definition.Residual);

        await EmitAsync(new AccessStep.Open(), cancellationToken);

        if (Definition.AllocationUnit is { } allocationUnit)
        {
            var index = await columnstoreService.GetIndex(allocationUnit, context.Database, cancellationToken);

            ColumnIds = ResolveColumns(index);

            Batch = CreateBatch(ResolveColumnNames(index));

            DeletedRows = await columnstoreService.GetDeletedRows(context.Database, index, cancellationToken);

            RowGroups = await EliminateRowGroupsAsync([.. index.CompressedRowGroups], cancellationToken);
        }
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

            var batch = await FillBatchAsync(RunLimit(RowOrdinal, Math.Min(BatchRows, remaining)), cancellationToken);

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

    private ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken)
        => Context.Steps.EmitAsync(step with { NodeId = NodeId }, cancellationToken);

    private void OpenBatch(RowGroup rowGroup)
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

    private ExecutionBatch CreateBatch(IReadOnlyList<string> columnNames)
    {
        BatchRows = BatchSize.GetRowCount(columnNames.Count + Definition.PipelineColumnNames.Count);

        var vectors = new List<BatchVector>(columnNames.Count + Definition.PipelineColumnNames.Count);

        OwnVectors.Clear();

        foreach (var name in columnNames)
        {
            var vector = new BatchVector(new BatchColumn { Name = name }, BatchRows);

            OwnVectors.Add(vector);

            vectors.Add(vector);
        }

        foreach (var name in Definition.PipelineColumnNames)
        {
            vectors.Add(new BatchVector(new BatchColumn { Name = name, DataType = SqlDbType.Variant }, BatchRows));
        }

        return new ExecutionBatch(BatchRows, vectors, new BatchDeepDataStore());
    }

    private async Task<ExecutionBatch?> FillBatchAsync(int size, CancellationToken cancellationToken)
    {
        var batch = Batch!;

        batch.Reset(size);

        var rowGroupId = batch.RowGroupId;

        var filterRleEntries = 0;

        var filterOperations = 0;

        var pureColumns = 0;

        var impureColumns = 0;

        RowMask.AsSpan(0, size).Fill(true);

        var deleted = ApplyDeletedRows(RowMask.AsSpan(0, size), rowGroupId, RowOrdinal);

        var filtered = ApplyCompressedFilters(RowMask.AsSpan(0, size), RowOrdinal, ref filterRleEntries, ref filterOperations);

        if (filtered > 0 || deleted > 0)
        {
            batch.SelectionVector.Set(RowMask.AsSpan(0, size));
        }

        var materialised = 0;

        if (batch.SelectionVector.RowCount > 0)
        {
            for (var i = 0; i < Columns.Count; i++)
            {
                FillVector(Columns[i], BoundVectors[i], RowOrdinal, RowMask.AsSpan(0, size), batch.DeepDataContext, ref materialised);
            }

            if (await ApplyPredicateAsync(batch, size, cancellationToken) > 0)
            {
                batch.SelectionVector.Set(RowMask.AsSpan(0, size));
            }

            ClassifyColumns(ref pureColumns, ref impureColumns);
        }

        if (deleted > 0)
        {
            await EmitAsync(new AccessStep.DeleteBitmapApplied(rowGroupId, deleted), cancellationToken);
        }

        RowOrdinal += size;

        if (batch.SelectionVector.RowCount == 0)
        {
            await EmitAsync(new AccessStep.BatchSkipped(rowGroupId, RowOrdinal - size, size)
            {
                FilterRleEntries = filterRleEntries,
                FilterOperations = filterOperations,
                HasCompressedFilter = HasCompressedFilter,
                HasPredicate = Predicate is not null || Definition.IsGenericFilterUsed
            },
                            cancellationToken);

            return null;
        }

        if (PushdownSink is { } sink)
        {
            var groupsBefore = sink.GroupCount;

            var isRunFolded = AccumulatePushdown(batch, sink);

            LocallyAggregatedRows += batch.SelectionVector.RowCount;

            await EmitAsync(new AccessStep.AggregatePushdown(rowGroupId,
                                                             RowOrdinal - size,
                                                             batch.SelectionVector.RowCount,
                                                             sink.GroupCount,
                                                             sink.GroupCount - groupsBefore,
                                                             isRunFolded),
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
            FilterRleEntries = filterRleEntries,
            FilterOperations = filterOperations,
            Materialised = materialised,
            HasCompressedFilter = HasCompressedFilter,
            HasPredicate = Predicate is not null || Definition.IsGenericFilterUsed,
            PureColumns = pureColumns,
            ImpureColumns = impureColumns
        },
                        cancellationToken);

        return batch;
    }

    private async Task<int> ApplyPredicateAsync(ExecutionBatch batch, int size, CancellationToken cancellationToken)
    {
        if (Predicate is null)
        {
            return 0;
        }

        var (evaluated, matches) = EvaluatePredicate(batch, size);

        VectorNumber++;

        await EmitAsync(new AccessStep.FilterVector(VectorNumber, batch.RowGroupId, PredicateColumnNames, evaluated, matches),
                        cancellationToken);

        return evaluated - matches;
    }

    private (int Evaluated, int Matches) EvaluatePredicate(ExecutionBatch batch, int size)
    {
        var mask = RowMask.AsSpan(0, size);

        Values.Bind(batch);

        var evaluated = 0;

        var matches = 0;

        for (var i = 0; i < mask.Length; i++)
        {
            if (!mask[i])
            {
                continue;
            }

            evaluated++;

            Values.MoveTo(i);

            if (PredicateEvaluator.Evaluate(Predicate!, Values, Context.EvaluationContext) == true)
            {
                matches++;

                continue;
            }

            mask[i] = false;
        }

        return (evaluated, matches);
    }

    private int ApplyDeletedRows(Span<bool> mask, int rowGroupId, int from)
    {
        var rows = DeletedRows.ForRowGroup(rowGroupId);

        if (rows.Length == 0)
        {
            return 0;
        }

        var start = Array.BinarySearch(rows, from);

        if (start < 0)
        {
            start = ~start;
        }

        var cleared = 0;

        for (var i = start; i < rows.Length && rows[i] < from + mask.Length; i++)
        {
            mask[rows[i] - from] = false;

            cleared++;
        }

        return cleared;
    }

    private int ApplyCompressedFilters(Span<bool> mask, int fromRow, ref int rleEntries, ref int operations)
    {
        var cleared = 0;

        foreach (var column in Columns)
        {
            if (column.Filter is not { } filter)
            {
                continue;
            }

            if (mask.IndexOf(true) < 0)
            {
                break;
            }

            foreach (var run in column.Reader.DataIds.GetRuns(fromRow, mask.Length))
            {
                var offset = run.FirstRow - fromRow;

                rleEntries++;

                if (run.Origin == SegmentValueOrigin.RleRun)
                {
                    operations++;

                    if (filter.IsMatch(run.Value))
                    {
                        continue;
                    }

                    var selected = mask.Slice(offset, run.RowCount);

                    cleared += selected.Count(true);

                    selected.Fill(false);

                    continue;
                }

                for (var i = 0; i < run.RowCount; i++)
                {
                    if (!mask[offset + i])
                    {
                        continue;
                    }

                    operations++;

                    if (filter.IsMatch(column.Reader.DataIds.GetRowDataId(run.FirstRow + i)))
                    {
                        continue;
                    }

                    mask[offset + i] = false;

                    cleared++;
                }
            }
        }

        return cleared;
    }

    private int RunLimit(int fromRow, int max)
    {
        var limit = max;

        foreach (var column in Columns)
        {
            foreach (var run in column.Reader.DataIds.GetRuns(fromRow, max))
            {
                if (run.Origin == SegmentValueOrigin.VariableLengthData)
                {
                    break;
                }

                if (run.RowCount > 0 && run.RowCount < limit)
                {
                    limit = run.RowCount;
                }

                break;
            }
        }

        return Math.Max(1, limit);
    }

    /// <summary>
    /// Aggregate Pushdown
    /// </summary>
    /// <remarks>
    /// Aggregate Pushdown is where aggregations are executed inside the columnstore scan directly.
    ///
    /// The scan reports no batches and no rows. What it folds is counted as locally aggregated instead, which is why a pushed down
    /// scan shows ActualRows and Batches of zero next to an ActualLocallyAggregatedRows of the whole table. SQL Server still emits
    /// batches, each carrying grouping keys and a partial aggregate rather than rows, they are simply not counted as batches. Here
    /// the values go straight into the sink, so that intermediate form is skipped.
    ///
    /// From the call stack in SQL Server:
    ///
    ///     RowBucketProcessorNew::FlushGroupedAggregateResults - Aggregate results being flushed
    ///
    ///       RowBucketProcessorNew::PushPartialAggregatesToQE - Partial aggregates are being pushed to the Query Engine
    ///
    ///         CBpagAggregateInMemory::AggregateBatchFastPath - Fast path = Aggregate Pushdown
    ///
    ///           CBpagBatchProcessing::RecomputeHashForGlobalAggregation - Partials folded into the global table
    ///
    /// The sink builder is the Hash Aggregate's local table, passed to this method so the current batch can be added to it. The
    /// global table stays with the Hash Aggregate and takes these partials at its merge, which is what RecomputeHashForGlobalAggregation
    /// does above.
    /// </remarks>
    private bool AccumulatePushdown(ExecutionBatch batch, HashAggregateBuilder sink)
    {
        var selection = batch.SelectionVector;

        if (CanFoldRun(batch, sink))
        {
            sink.AccumulateRun(BatchRecordBuilder.Build(batch, selection[0]),
                               PushdownContext!,
                               selection.RowCount,
                               null,
                               0);

            return true;
        }

        for (var index = 0; index < selection.RowCount; index++)
        {
            sink.Accumulate(BatchRecordBuilder.Build(batch, selection[index]), PushdownContext!);
        }

        return false;
    }

    /// <summary>
    /// Checks if a batch can be folded into a single operation
    /// </summary>
    /// <remarks>
    /// A. Every GROUP BY column must be constant
    ///
    /// B. No aggregate has an argument
    ///
    /// C. RowCount != 0 - guard against empty vectors
    ///
    /// If both of those conditions are met the row count and constant value can be used together for aggregations.
    /// </remarks>
    private static bool CanFoldRun(ExecutionBatch batch, HashAggregateBuilder sink)
    {
        if (batch.SelectionVector.RowCount == 0 || sink.Aggregates.Any(a => a.Argument is not null))
        {
            return false;
        }

        foreach (var column in sink.GroupBy)
        {
            var vector = batch.Vectors.FirstOrDefault(v => string.Equals(v.Column.Name,
                                                                        column,
                                                                        StringComparison.OrdinalIgnoreCase));

            if (vector is not { IsConstant: true })
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Classifies a column into Pure or Impure
    /// </summary>
    /// <remarks>
    /// Pure - Pure RLE run sourced values - All runs are Origin = SegmentValueOrigin.RleRun
    ///
    /// Impure - Values that are not exclusively sourced from RLE runs, e.g. RLE + Bitpack
    ///
    /// The classification is there because knowing a run is Pure opens up optimizations that work directly from the runs rather than
    /// materialized values. There are also other optimization, not implemented here, such as optimized fills that avoid branch switching
    /// designed to give a further edge to the batch performance.
    /// </remarks>
    private void ClassifyColumns(ref int pure, ref int impure)
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            if (BoundVectors[i].IsConstant)
            {
                pure++;

                continue;
            }

            impure++;
        }
    }

    /// <summary>
    /// Fills a vector with values for a column
    /// </summary>
    /// <remarks>
    /// The fill has two paths:
    ///
    /// 1. Optimized RLE path
    ///    - If all values are all the same and no rows are masked same the vector is set as a constant value with no fill
    ///    - Else all values are filled with the single materialized value
    ///
    /// 2. Non-RLE path - value is constructed per row
    /// </remarks>
    private static void FillVector(ScanColumn column,
                                   BatchVector vector,
                                   int fromRow,
                                   ReadOnlySpan<bool> mask,
                                   IDeepDataContext deepDataContext,
                                   ref int materialisedCount)
    {
        foreach (var run in column.Reader.DataIds.GetRuns(fromRow, mask.Length))
        {
            var offset = run.FirstRow - fromRow;

            if (run.Origin == SegmentValueOrigin.RleRun)
            {
                if (mask.Slice(offset, run.RowCount).IndexOf(true) < 0)
                {
                    continue;
                }

                var batchValue = CreateBatchValue(column, run.Value, run.FirstRow, deepDataContext);

                materialisedCount++;

                if (offset == 0 && run.RowCount == mask.Length)
                {
                    vector.SetConstant(batchValue);

                    continue;
                }

                vector.Values.AsSpan(offset, run.RowCount).Fill(batchValue);

                continue;
            }

            for (var i = 0; i < run.RowCount; i++)
            {
                if (!mask[offset + i])
                {
                    continue;
                }

                var rowOrdinal = run.FirstRow + i;

                materialisedCount++;

                vector.SetValue(offset + i,
                                CreateBatchValue(column,
                                                 column.Reader.DataIds.GetRowDataId(rowOrdinal),
                                                 rowOrdinal,
                                                 deepDataContext));
            }
        }
    }

    private static BatchValue CreateBatchValue(ScanColumn column, long dataId, int rowOrdinal, IDeepDataContext deepData)
    {
        var segment = column.Reader.Segment;

        if (segment.HasNulls && segment.NullValue == dataId)
        {
            return BatchValueNormalizer.Null;
        }

        if (column is { HasDictionary: true, Column.Domain: BatchValueDomain.Dictionary })
        {
            return BatchValueNormalizer.FromDictionaryDataId(dataId);
        }

        var raw = column.Reader.GetRawValue(rowOrdinal);

        if (raw is byte[] bytes)
        {
            return new BatchValue(deepData.Store(bytes));
        }

        var value = ColumnstoreValueConverter.Convert(raw, segment.Column?.Structure);

        if (BatchValueNormalizer.TryNormalizeValue(value, out var slot))
        {
            return slot;
        }

        return new BatchValue(deepData.Store(ToDeepBytes(value)));
    }

    private async Task<bool> MoveToNextRowGroupAsync(CancellationToken cancellationToken)
    {
        while (RowGroupIndex < RowGroups.Count)
        {
            var rowGroup = RowGroups[RowGroupIndex++];

            var reader = await OpenRowGroupAsync(rowGroup, cancellationToken);

            var columns = Bind(reader, rowGroup);

            if (ColumnIds.Count > 0 && (columns.Count == 0 || reader.RowCount == 0))
            {
                await EmitAsync(new AccessStep.RowGroupSkipped(rowGroup.RowGroupId, "No readable segments"), cancellationToken);

                continue;
            }

            if (UnmatchedDictionaryColumn(reader) is { } unmatched)
            {
                await EliminateOnDictionaryAsync(rowGroup, reader, unmatched, cancellationToken);

                continue;
            }

            Reader = reader;

            Columns = columns;

            Predicate = ResolvePredicate(columns);

            PredicateColumnNames = Predicate is null
                                   ? string.Empty
                                   : string.Join(", ", PredicateColumns.Referenced(Predicate).Distinct());

            OpenBatch(rowGroup);

            RowGroupRowCount = ColumnIds.Count > 0 ? reader.RowCount : rowGroup.TotalRows;

            RowOrdinal = 0;

            await EmitAsync(new AccessStep.RowGroupOpened(rowGroup.RowGroupId, columns.Count, BatchRows), cancellationToken);

            if (columns.Where(c => c.Filter is not null).Select(c => c.Column.Name).ToList() is { Count: > 0 } filtered)
            {
                await EmitAsync(new AccessStep.CompressedDataFilter(rowGroup.RowGroupId, string.Join(", ", filtered), true),
                                cancellationToken);
            }
            else if (Predicate is not null)
            {
                await EmitAsync(new AccessStep.CompressedDataFilter(rowGroup.RowGroupId, PredicateColumnNames, false),
                                cancellationToken);
            }

            return true;
        }

        return false;
    }

    private async Task<List<RowGroup>> EliminateRowGroupsAsync(IReadOnlyList<RowGroup> rowGroups, CancellationToken cancellationToken)
    {
        var qualified = new List<RowGroup>(rowGroups.Count);

        foreach (var rowGroup in rowGroups)
        {
            var partition = Partitions.Evaluate(rowGroup);

            if (partition.IsEliminated)
            {
                if (SkippedPartitions.Add(rowGroup.PartitionId))
                {
                    await EmitAsync(new AccessStep.PartitionSkipped(rowGroup.PartitionId, partition.Reason), cancellationToken);
                }

                continue;
            }

            if (await IsEliminatedAsync(rowGroup, cancellationToken))
            {
                continue;
            }

            qualified.Add(rowGroup);
        }

        return qualified;
    }

    private async Task<bool> IsEliminatedAsync(RowGroup rowGroup, CancellationToken cancellationToken)
    {
        var projected = rowGroup.Segments
                                .Where(s => s.Column is not null && ColumnIds.Contains(s.Column.ColumnStoreColumnId))
                                .ToList();

        var eliminated = new List<string>();

        foreach (var segment in projected)
        {
            var result = Segments.Evaluate(segment);

            if (!result.IsEliminated)
            {
                continue;
            }

            eliminated.Add(segment.Column!.Name);

            await EmitAsync(new AccessStep.SegmentSkipped(rowGroup.RowGroupId,
                                                          segment.Column!.ColumnStoreColumnId,
                                                          segment.Column!.Name,
                                                          result.Reason),
                            cancellationToken);
        }

        if (projected.Count > 0)
        {
            await EmitAsync(new AccessStep.SegmentElimination(rowGroup.RowGroupId, eliminated.Count, projected.Count),
                            cancellationToken);
        }

        if (eliminated.Count == 0)
        {
            return false;
        }

        var columns = string.Join(", ", eliminated);

        var label = eliminated.Count == 1 ? "segment" : "segments";

        var message = $"No match possible in {eliminated.Count} of {projected.Count} {label} ({columns})";

        await EmitAsync(new AccessStep.RowGroupSkipped(rowGroup.RowGroupId, message),
                        cancellationToken);

        return true;
    }

    private async Task EliminateOnDictionaryAsync(RowGroup rowGroup,
                                                  RowGroupReader reader,
                                                  SegmentReader unmatched,
                                                  CancellationToken cancellationToken)
    {
        var column = unmatched.Segment.Column!;

        var projected = reader.Readers.Count(r => r.Segment.Column is not null);

        await EmitAsync(new AccessStep.SegmentSkipped(rowGroup.RowGroupId,
                                                      column.ColumnStoreColumnId,
                                                      column.Name,
                                                      "No dictionary entry matches"),
                        cancellationToken);

        await EmitAsync(new AccessStep.SegmentElimination(rowGroup.RowGroupId, 1, projected), cancellationToken);

        await EmitAsync(new AccessStep.RowGroupSkipped(rowGroup.RowGroupId,
                                                       $"No match possible in 1 of {projected} segments ({column.Name})"),
                        cancellationToken);
    }

    private async Task<RowGroupReader> OpenRowGroupAsync(RowGroup rowGroup, CancellationToken cancellationToken)
    {
        var wanted = ColumnIds.Count == 0
                     ? []
                     : rowGroup.Segments
                               .Where(s => s.Column is null || ColumnIds.Contains(s.Column.ColumnStoreColumnId))
                               .ToList();

        var readers = new List<SegmentReader>();

        var skipped = new List<ColumnSegment>();

        for (var i = 0; i < wanted.Count; i++)
        {
            var segment = wanted[i];

            foreach (var dictionary in GetSegmentDictionaries(segment))
            {
                if (dictionary.IsGlobal
                    && !OpenDictionaries.Add((dictionary.HobtId, dictionary.ColumnId, dictionary.DictionaryId)))
                {
                    continue;
                }

                await EmitAsync(new AccessStep.DictionaryOpened(rowGroup.RowGroupId,
                                                                segment.Column?.ColumnStoreColumnId ?? -1,
                                                                segment.Column?.Name ?? string.Empty,
                                                                dictionary.IsGlobal,
                                                                dictionary.EntryCount,
                                                                dictionary.OnDiskSize),
                                cancellationToken);
            }

            await EmitAsync(new AccessStep.SegmentOpened(rowGroup.RowGroupId,
                                                         segment.Column?.ColumnStoreColumnId ?? -1,
                                                         segment.Column?.Name ?? string.Empty,
                                                         segment.OnDiskSize),
                            cancellationToken);

            try
            {
                readers.Add(await columnstoreService.GetSegmentReader(Context.Database, segment, cancellationToken));
            }
            catch (InvalidDataException)
            {
                skipped.Add(segment);
            }
        }

        return new RowGroupReader(rowGroup, readers, skipped);
    }

    private SegmentReader? UnmatchedDictionaryColumn(RowGroupReader reader)
    {
        if (Definition.Residual is not { } residual)
        {
            return null;
        }

        foreach (var segmentReader in reader.Readers)
        {
            if (segmentReader.Segment is not { Column.Structure: not null } segment)
            {
                continue;
            }

            if (segment is { PrimaryDictionaryId: < 0, SecondaryDictionaryId: < 0 })
            {
                continue;
            }

            if (CompressedDataFilter.MatchingDictionaryIds(residual, segmentReader, Context.EvaluationContext) is { Count: 0 })
            {
                return segmentReader;
            }
        }

        return null;
    }

    private static IEnumerable<SegmentDictionary> GetSegmentDictionaries(ColumnSegment segment)
    {
        if (segment is { PrimaryDictionaryId: >= 0, Column.GlobalDictionary: { } global })
        {
            yield return global;
        }

        if (segment is { SecondaryDictionaryId: >= 0, LocalDictionary: { } local })
        {
            yield return local;
        }
    }

    private List<ScanColumn> Bind(RowGroupReader reader, RowGroup rowGroup)
    {
        var columns = new List<ScanColumn>();

        foreach (var columnId in ColumnIds)
        {
            var segmentReader = reader.Readers.FirstOrDefault(r => r.Segment.Column?.ColumnStoreColumnId == columnId);

            if (segmentReader?.Segment.Column?.Structure is not { } structure)
            {
                continue;
            }

            var segment = segmentReader.Segment;

            var hasLocal = segment.SecondaryDictionaryId >= 0;

            var hasDictionary = hasLocal || segment.PrimaryDictionaryId >= 0;

            var column = new BatchColumn
            {
                Name = structure.ColumnName,
                DataType = structure.DataType,
                Precision = structure.Precision,
                Scale = structure.Scale,
                DataLength = structure.DataLength
            };

            if (hasDictionary && column.Domain == BatchValueDomain.Dictionary)
            {
                column.IdSpace = new DataIdSpace(rowGroup.HobtId,
                                                 columnId,
                                                 hasLocal ? segment.SecondaryDictionaryId : DataIdSpace.NoLocalDictionary);
            }

            var filter = Definition.IsFilterOnCompressedDataUsed
                            ? CompressedDataFilter.Create(Definition.Residual, segmentReader, Context.EvaluationContext)
                            : null;

            columns.Add(new ScanColumn(segmentReader, column, hasDictionary, filter));
        }

        return columns;
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

    private static byte[] ToDeepBytes(object? value) => value switch
    {
        byte[] bytes => bytes,
        long number => BitConverter.GetBytes(number),
        double number => BitConverter.GetBytes(number),
        SqlDecimal number => number.BinData,
        _ => []
    };

    private void Reset()
    {
        RowGroupIndex = 0;

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

        SkippedPartitions.Clear();

        OpenDictionaries.Clear();
    }

    private sealed record ScanColumn(SegmentReader Reader,
                                     BatchColumn Column,
                                     bool HasDictionary,
                                     CompressedDataFilter? Filter);
}
