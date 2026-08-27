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
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Services;

namespace InternalsViewer.Execution.Iterators.BatchMode.DataAccess;

/// <summary>
/// Reads compressed row groups of a columnstore index as batches
/// </summary>
/// <remarks>
/// 
/// </remarks>
public sealed class ColumnstoreScanIterator(ColumnstoreService columnstoreService) : IBatchIterator
{
    public int NodeId { get; private set; }

    public bool IsComplete { get; private set; }

    public StopReason? StopReason { get; private set; }

    private IteratorContext Context { get; set; } = null!;

    private ColumnstoreScanDefinition Definition { get; set; } = null!;

    private List<ScanColumn> Columns { get; set; } = [];

    private RowGroupReader? Reader { get; set; }

    private int RowGroupIndex { get; set; }

    private int RowOrdinal { get; set; }

    private long BatchNumber { get; set; }

    private DeletedRows DeletedRows { get; set; } = DeletedRows.None;

    private List<RowGroup> RowGroups { get; set; } = [];

    private IReadOnlyList<int> ColumnIds { get; set; } = [];

    private int BatchRows { get; set; } = BatchSize.MaxRowCount;

    private PartitionEliminator Partitions { get; set; } = new(null);

    private SegmentEliminator Segments { get; set; } = new(null);

    private HashSet<long> SkippedPartitions { get; } = [];

    private HashSet<(long HobtId, int ColumnId, int DictionaryId)> OpenDictionaries { get; } = [];

    private bool[] RowMask { get; } = new bool[BatchSize.MaxRowCount];

    private bool HasCompressedFilter => Columns.Exists(c => c.Filter is not null);

    private BatchRowValueSource Values { get; } = new();

    private ExecutionBatch? Batch { get; set; }

    private AccessPredicate? Predicate { get; set; }

    private string PredicateColumnNames { get; set; } = string.Empty;

    private long VectorNumber { get; set; }

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

            DeletedRows = await columnstoreService.GetDeletedRows(context.Database, index, cancellationToken);

            RowGroups = await EliminateRowGroupsAsync([.. index.CompressedRowGroups], cancellationToken);
        }
    }

    public async Task<ExecutionBatch?> GetNextBatchAsync(CancellationToken cancellationToken)
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

            var remaining = Reader!.RowCount - RowOrdinal;

            if (remaining <= 0)
            {
                Reader = null;

                continue;
            }

            var batch = await FillBatchAsync(Math.Min(BatchRows, remaining), cancellationToken);

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

    private ExecutionBatch OpenBatch(RowGroup rowGroup)
    {
        if (Batch is not { } batch || batch.Vectors.Count != Columns.Count || batch.Capacity != BatchRows)
        {
            return CreateBatch(rowGroup);
        }

        batch.RowGroupId = rowGroup.RowGroupId;

        for (var i = 0; i < Columns.Count; i++)
        {
            batch.Vectors[i].Column = Columns[i].Column;

            batch.Vectors[i].Source = Columns[i].Reader;
        }

        return batch;
    }

    private ExecutionBatch CreateBatch(RowGroup rowGroup)
    {
        var vectors = new List<BatchVector>(Columns.Count);

        foreach (var column in Columns)
        {
            vectors.Add(new BatchVector(column.Column, BatchRows) { Source = column.Reader });
        }

        return new ExecutionBatch(BatchRows, vectors, new BatchDeepDataStore()) { RowGroupId = rowGroup.RowGroupId };
    }

    private async Task<ExecutionBatch?> FillBatchAsync(int size, CancellationToken cancellationToken)
    {
        var batch = Batch!;

        batch.Reset(size);

        var rowGroupId = batch.RowGroupId;

        var filterRleEntries = 0;

        var filterOperations = 0;

        RowMask.AsSpan(0, size).Fill(true);

        var deleted = ApplyDeletedRows(RowMask.AsSpan(0, size), rowGroupId, RowOrdinal, size);

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
                FillVector(Columns[i], batch.Vectors[i], RowOrdinal, RowMask.AsSpan(0, size), batch.DeepDataContext, ref materialised);
            }

            if (await ApplyPredicateAsync(batch, size, cancellationToken) > 0)
            {
                batch.SelectionVector.Set(RowMask.AsSpan(0, size));
            }
        }

        if (deleted > 0)
        {
            await EmitAsync(new AccessStep.DeleteBitmapApplied(rowGroupId, deleted), cancellationToken);
        }

        RowOrdinal += size;

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
                            HasPredicate = Predicate is not null || Definition.IsGenericFilterUsed
                        },
                        cancellationToken);

        return batch.SelectionVector.RowCount == 0 ? null : batch;
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

    private int ApplyDeletedRows(Span<bool> mask, int rowGroupId, int from, int size)
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

        for (var i = start; i < rows.Length && rows[i] < from + size; i++)
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

            foreach (var run in column.Reader.DataIds.GetRuns(fromRow, mask.Length))
            {
                var offset = run.FirstRow - fromRow;

                rleEntries++;

                if (run.Origin == SegmentValueOrigin.RleRun)
                {
                    operations++;

                    if (filter.Matches(run.Value))
                    {
                        continue;
                    }

                    for (var i = 0; i < run.RowCount; i++)
                    {
                        if (!mask[offset + i])
                        {
                            continue;
                        }

                        mask[offset + i] = false;

                        cleared++;
                    }

                    continue;
                }

                for (var i = 0; i < run.RowCount; i++)
                {
                    if (!mask[offset + i])
                    {
                        continue;
                    }

                    operations++;

                    if (filter.Matches(column.Reader.DataIds.GetRowDataId(run.FirstRow + i)))
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

    private static void FillVector(ScanColumn column,
                                   BatchVector vector,
                                   int fromRow,
                                   ReadOnlySpan<bool> mask,
                                   IDeepDataContext deepData,
                                   ref int materialised)
    {
        foreach (var run in column.Reader.DataIds.GetRuns(fromRow, mask.Length))
        {
            var offset = run.FirstRow - fromRow;

            if (run.Origin == SegmentValueOrigin.RleRun)
            {
                var slot = default(BatchSlot);

                var decoded = false;

                for (var i = 0; i < run.RowCount; i++)
                {
                    if (!mask[offset + i])
                    {
                        continue;
                    }

                    if (!decoded)
                    {
                        slot = CreateSlot(column, run.Value, run.FirstRow, deepData);

                        materialised++;

                        decoded = true;
                    }

                    vector.Slots[offset + i] = slot;
                }

                continue;
            }

            for (var i = 0; i < run.RowCount; i++)
            {
                if (!mask[offset + i])
                {
                    continue;
                }

                var rowOrdinal = run.FirstRow + i;

                materialised++;

                vector.Slots[offset + i] = CreateSlot(column,
                                                      column.Reader.DataIds.GetRowDataId(rowOrdinal),
                                                      rowOrdinal,
                                                      deepData);
            }
        }
    }

    private static BatchSlot CreateSlot(ScanColumn column, long dataId, int rowOrdinal, IDeepDataContext deepData)
    {
        var segment = column.Reader.Segment;

        if (segment.HasNulls && segment.NullValue == dataId)
        {
            return BatchSlotNormalizer.Null;
        }

        if (column is { HasDictionary: true, Column.Domain: BatchSlotDomain.Dictionary })
        {
            return BatchSlotNormalizer.FromDictionaryDataId(dataId);
        }

        var raw = column.Reader.GetRawValue(rowOrdinal);

        if (raw is byte[] bytes)
        {
            return new BatchSlot(deepData.Store(bytes));
        }

        var value = ColumnstoreValueConverter.Convert(raw, segment.Column?.Structure);

        if (BatchSlotNormalizer.TryNormalizeValue(value, out var slot))
        {
            return slot;
        }

        return new BatchSlot(deepData.Store(ToDeepBytes(value)));
    }

    private async Task<bool> MoveToNextRowGroupAsync(CancellationToken cancellationToken)
    {
        while (RowGroupIndex < RowGroups.Count)
        {
            var rowGroup = RowGroups[RowGroupIndex++];

            var reader = await OpenRowGroupAsync(rowGroup, cancellationToken);

            var columns = Bind(reader, rowGroup);

            if (columns.Count == 0 || reader.RowCount == 0)
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

            BatchRows = BatchSize.GetRowCount(columns.Count);

            Predicate = ResolvePredicate(columns);

            PredicateColumnNames = Predicate is null
                                   ? string.Empty
                                   : string.Join(", ", PredicateColumns.Referenced(Predicate).Distinct());

            Batch = OpenBatch(rowGroup);

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

        await EmitAsync(new AccessStep.SegmentElimination(rowGroup.RowGroupId, eliminated.Count, projected.Count),
                        cancellationToken);

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
        var wanted = rowGroup.Segments
                             .Where(s => s.Column is null || ColumnIds.Contains(s.Column.ColumnStoreColumnId))
                             .ToList();

        var readers = new List<SegmentReader>();

        var skipped = new List<ColumnSegment>();

        for (var i = 0; i < wanted.Count; i++)
        {
            var segment = wanted[i];

            foreach (var dictionary in Dictionaries(segment))
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
            if (segmentReader.Segment is not { Column.Structure: { } structure } segment)
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

    private static IEnumerable<SegmentDictionary> Dictionaries(ColumnSegment segment)
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

            if (hasDictionary && column.Domain == BatchSlotDomain.Dictionary)
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

        if (!CompressedDataFilter.IsPureConjunction(residual))
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

    private List<int> ResolveColumns(ColumnStoreIndex index)
    {
        if (Definition.ColumnNames.Count == 0)
        {
            return [.. index.Columns.Where(c => !c.IsInternal).Select(c => c.ColumnStoreColumnId)];
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
