using System.Data.SqlTypes;
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
public sealed class ColumnstoreScanIterator(ColumnstoreService columnstoreService) : IBatchIterator
{
    /// <summary>
    /// Row positions a batch holds, sized so every vector for a batch sits in cache together
    /// </summary>
    public const int BatchBytes = 65536;

    public const int MinimumBatchRows = 64;

    public const int MaximumBatchRows = 900;

    private const int SlotBytes = 8;

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

    private DeletedRows Deleted { get; set; } = DeletedRows.None;

    private List<RowGroup> RowGroups { get; set; } = [];

    private IReadOnlyList<int> ColumnIds { get; set; } = [];

    private int BatchRows { get; set; } = MaximumBatchRows;

    private PartitionEliminator Partitions { get; set; } = new(null);

    private SegmentEliminator Segments { get; set; } = new(null);

    private HashSet<long> SkippedPartitions { get; } = [];

    private bool[] Keep { get; } = new bool[MaximumBatchRows];

    private ExecutionBatch? Batch { get; set; }

    public async Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken)
    {
        Definition = definition.Expect<ColumnstoreScanDefinition>();

        Context = context;

        NodeId = definition.NodeId;

        RowGroupIndex = 0;

        RowOrdinal = 0;

        BatchNumber = 0;

        Reader = null;

        Columns = [];

        Batch = null;

        IsComplete = false;

        StopReason = null;

        RowGroups = [];

        ColumnIds = [];

        Deleted = DeletedRows.None;

        Partitions = new PartitionEliminator(Definition.Residual);

        Segments = new SegmentEliminator(Definition.Residual);

        SkippedPartitions.Clear();

        await EmitAsync(new AccessStep.Open(), cancellationToken);

        if (Definition.AllocationUnit is { } allocationUnit)
        {
            var index = await columnstoreService.GetIndex(allocationUnit, context.Database, cancellationToken);

            ColumnIds = ResolveColumns(index);

            Deleted = await columnstoreService.GetDeletedRows(context.Database, index, cancellationToken);

            RowGroups = await QualifyAsync([.. index.CompressedRowGroups], cancellationToken);
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

            var batch = await FillAsync(Math.Min(BatchRows, remaining), cancellationToken);

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

    private ExecutionBatch CreateBatch(RowGroup rowGroup)
    {
        var vectors = new List<BatchVector>(Columns.Count);

        foreach (var column in Columns)
        {
            vectors.Add(new BatchVector(column.Column, BatchRows) { Source = column.Reader });
        }

        return new ExecutionBatch(BatchRows, vectors, new BatchDeepDataStore()) { RowGroupId = rowGroup.RowGroupId };
    }

    private async Task<ExecutionBatch?> FillAsync(int size, CancellationToken cancellationToken)
    {
        var batch = Batch!;

        batch.Reset(size);

        var rleEntries = 0;

        var operations = 0;

        for (var i = 0; i < Columns.Count; i++)
        {
            FillVector(Columns[i], batch.Vectors[i], RowOrdinal, size, batch.DeepDataContext, ref rleEntries, ref operations);
        }

        var rowGroupId = batch.RowGroupId;

        var keep = Keep.AsSpan(0, size);

        keep.Fill(true);

        var filtered = ApplyCompressedFilters(keep, RowOrdinal, ref rleEntries, ref operations);

        var deleted = ApplyDeleted(keep, rowGroupId, RowOrdinal, size);

        if (filtered > 0 || deleted > 0)
        {
            Select(batch.SelectionVector, keep);
        }

        if (deleted > 0)
        {
            await EmitAsync(new AccessStep.DeleteBitmapApplied(rowGroupId, deleted), cancellationToken);
        }

        RowOrdinal += size;

        if (batch.SelectionVector.RowCount == 0)
        {
            return null;
        }

        BatchNumber++;

        await EmitAsync(new AccessStep.BatchProduced(BatchNumber,
                                                     rowGroupId,
                                                     RowOrdinal - size,
                                                     size,
                                                     batch.SelectionVector.RowCount,
                                                     rleEntries,
                                                     operations),
                        cancellationToken);

        return batch;
    }

    private static void Select(SelectionVector selection, ReadOnlySpan<bool> keep)
    {
        selection.RemoveAll();

        for (var i = 0; i < keep.Length; i++)
        {
            if (keep[i])
            {
                selection.Add(i);
            }
        }
    }

    private int ApplyDeleted(Span<bool> keep, int rowGroupId, int from, int size)
    {
        var rows = Deleted.ForRowGroup(rowGroupId);

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
            keep[rows[i] - from] = false;

            cleared++;
        }

        return cleared;
    }

    private int ApplyCompressedFilters(Span<bool> keep, int fromRow, ref int rleEntries, ref int operations)
    {
        var cleared = 0;

        foreach (var column in Columns)
        {
            if (column.Filter is not { } filter)
            {
                continue;
            }

            foreach (var run in column.Reader.DataIds.GetRuns(fromRow, keep.Length))
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

                    keep.Slice(offset, run.RowCount).Clear();

                    cleared += run.RowCount;

                    continue;
                }

                operations += run.RowCount;

                for (var i = 0; i < run.RowCount; i++)
                {
                    if (filter.Matches(column.Reader.DataIds.GetRowDataId(run.FirstRow + i)))
                    {
                        continue;
                    }

                    keep[offset + i] = false;

                    cleared++;
                }
            }
        }

        return cleared;
    }

    private static void FillVector(ScanColumn column,
                                   BatchVector vector,
                                   int fromRow,
                                   int size,
                                   IDeepDataContext deepData,
                                   ref int rleEntries,
                                   ref int operations)
    {
        foreach (var run in column.Reader.DataIds.GetRuns(fromRow, size))
        {
            var offset = run.FirstRow - fromRow;

            rleEntries++;

            if (run.Origin == SegmentValueOrigin.RleRun)
            {
                operations++;

                var slot = CreateSlot(column, run.Value, run.FirstRow, deepData);

                for (var i = 0; i < run.RowCount; i++)
                {
                    vector.Slots[offset + i] = slot;
                }

                continue;
            }

            operations += run.RowCount;

            for (var i = 0; i < run.RowCount; i++)
            {
                var rowOrdinal = run.FirstRow + i;

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

            Reader = reader;

            Columns = columns;

            BatchRows = GetBatchRows(columns.Count);

            Batch = CreateBatch(rowGroup);

            RowOrdinal = 0;

            await EmitAsync(new AccessStep.RowGroupOpened(rowGroup.RowGroupId, columns.Count, BatchRows), cancellationToken);

            if (columns.Where(c => c.Filter is not null).Select(c => c.Column.Name).ToList() is { Count: > 0 } filtered)
            {
                await EmitAsync(new AccessStep.CompressedDataFilter(rowGroup.RowGroupId, string.Join(", ", filtered)),
                                cancellationToken);
            }

            return true;
        }

        return false;
    }

    private async Task<List<RowGroup>> QualifyAsync(IReadOnlyList<RowGroup> rowGroups, CancellationToken cancellationToken)
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

        await EmitAsync(new AccessStep.RowGroupSkipped(rowGroup.RowGroupId,
                                                       $"No match possible in {eliminated.Count} of {projected.Count} "
                                                       + $"{label} ({columns})"),
                        cancellationToken);

        return true;
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

            try
            {
                readers.Add(await columnstoreService.GetSegmentReader(Context.Database, segment, cancellationToken));
            }
            catch (InvalidDataException)
            {
                skipped.Add(segment);
            }

            await EmitAsync(new AccessStep.SegmentOpened(rowGroup.RowGroupId,
                                                         segment.Column?.ColumnStoreColumnId ?? -1,
                                                         segment.Column?.Name ?? string.Empty,
                                                         segment.OnDiskSize),
                            cancellationToken);
        }

        return new RowGroupReader(rowGroup, readers, skipped);
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

            columns.Add(new ScanColumn(segmentReader,
                                       column,
                                       hasDictionary,
                                       CompressedDataFilter.Create(Definition.Residual, segment)));
        }

        return columns;
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

    private static int GetBatchRows(int columns)
        => columns <= 0
            ? MaximumBatchRows
            : Math.Clamp(BatchBytes / (SlotBytes * columns), MinimumBatchRows, MaximumBatchRows);

    private static byte[] ToDeepBytes(object? value) => value switch
    {
        byte[] bytes => bytes,
        long number => BitConverter.GetBytes(number),
        double number => BitConverter.GetBytes(number),
        SqlDecimal number => number.BinData,
        _ => []
    };

    private sealed record ScanColumn(SegmentReader Reader,
                                     BatchColumn Column,
                                     bool HasDictionary,
                                     CompressedDataFilter? Filter);
}
