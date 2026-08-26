using System.Data.SqlTypes;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Elimination;
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

    private IteratorContext Context { get; set; } = null!;

    private ColumnstoreScanDefinition Definition { get; set; } = null!;

    private List<ScanColumn> Columns { get; set; } = [];

    private RowGroupReader? Reader { get; set; }

    private int RowGroupIndex { get; set; }

    private int RowOrdinal { get; set; }

    private DeletedRows Deleted { get; set; } = DeletedRows.None;

    private int BatchRows { get; set; } = MaximumBatchRows;

    private PartitionEliminator Partitions { get; set; } = new(null);

    private SegmentEliminator Segments { get; set; } = new(null);

    private HashSet<long> SkippedPartitions { get; } = [];

    public async Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken)
    {
        Definition = definition.Expect<ColumnstoreScanDefinition>();

        Context = context;

        NodeId = definition.NodeId;

        RowGroupIndex = 0;

        RowOrdinal = 0;

        Reader = null;

        Columns = [];

        IsComplete = false;

        Deleted = Definition.Index is { } index
                  ? await columnstoreService.GetDeletedRows(context.Database, index, cancellationToken)
                  : DeletedRows.None;

        Partitions = new PartitionEliminator(Definition.Residual);

        Segments = new SegmentEliminator(Definition.Residual);

        SkippedPartitions.Clear();

        await EmitAsync(new AccessStep.Open(), cancellationToken);
    }

    public async Task<ExecutionBatch?> GetNextBatchAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Reader is null && !await MoveToNextRowGroupAsync(cancellationToken))
            {
                IsComplete = true;

                await EmitAsync(new AccessStep.Stopped(StopReason.PageExhausted), cancellationToken);

                return null;
            }

            var remaining = Reader!.RowCount - RowOrdinal;

            if (remaining <= 0)
            {
                Reader = null;

                continue;
            }

            return await FillAsync(Math.Min(BatchRows, remaining), cancellationToken);
        }
    }

    public async Task CloseAsync()
    {
        Reader = null;

        Columns = [];

        IsComplete = true;

        await EmitAsync(new AccessStep.Close(), CancellationToken.None);
    }

    private ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken)
        => Context.Steps.EmitAsync(step with { NodeId = NodeId }, cancellationToken);

    private async Task<ExecutionBatch> FillAsync(int size, CancellationToken cancellationToken)
    {
        var deepData = new BatchDeepDataStore();

        var vectors = new List<BatchVector>(Columns.Count);

        foreach (var column in Columns)
        {
            var vector = new BatchVector(column.Column, size) { Source = column.Reader };

            for (var i = 0; i < size; i++)
            {
                vector.Slots[i] = CreateSlot(column, RowOrdinal + i, deepData);
            }

            vectors.Add(vector);
        }

        var rowGroupId = Reader!.RowGroup.RowGroupId;

        var batch = new ExecutionBatch(size, vectors, deepData) { RowGroupId = rowGroupId };

        var deleted = ClearDeleted(batch, rowGroupId, RowOrdinal, size);

        if (deleted > 0)
        {
            await EmitAsync(new AccessStep.DeletedRowsSkipped(rowGroupId, deleted), cancellationToken);
        }

        await EmitAsync(new AccessStep.BatchProduced(rowGroupId, RowOrdinal, size, batch.SelectionBitmap.Count),
                        cancellationToken);

        RowOrdinal += size;

        return batch;
    }

    private int ClearDeleted(ExecutionBatch batch, int rowGroupId, int from, int size)
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
            batch.SelectionBitmap.Clear(rows[i] - from);

            cleared++;
        }

        return cleared;
    }

    private static BatchSlot CreateSlot(ScanColumn column, int rowOrdinal, BatchDeepDataStore deepData)
    {
        var segment = column.Reader.Segment;

        var dataId = column.Reader.DataIds.GetDataId(rowOrdinal);

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
        while (RowGroupIndex < Definition.RowGroups.Count)
        {
            var rowGroup = Definition.RowGroups[RowGroupIndex++];

            var partition = Partitions.Evaluate(rowGroup);

            if (partition.IsEliminated)
            {
                if (SkippedPartitions.Add(rowGroup.PartitionId))
                {
                    await EmitAsync(new AccessStep.PartitionSkipped(rowGroup.PartitionId, partition.Reason), cancellationToken);
                }

                continue;
            }

            var reader = await columnstoreService.GetRowGroupReader(Context.Database, rowGroup, cancellationToken);

            var columns = Bind(reader, rowGroup);

            if (columns.Count == 0 || reader.RowCount == 0)
            {
                await EmitAsync(new AccessStep.RowGroupSkipped(rowGroup.RowGroupId, "No readable segments"), cancellationToken);

                continue;
            }

            if (await IsEliminatedAsync(rowGroup, columns, cancellationToken))
            {
                continue;
            }

            Reader = reader;

            Columns = columns;

            BatchRows = GetBatchRows(columns.Count);

            RowOrdinal = 0;

            await EmitAsync(new AccessStep.RowGroupOpened(rowGroup.RowGroupId, reader.RowCount, columns.Count, BatchRows),
                            cancellationToken);

            return true;
        }

        return false;
    }

    private async Task<bool> IsEliminatedAsync(RowGroup rowGroup, List<ScanColumn> columns, CancellationToken cancellationToken)
    {
        foreach (var column in columns)
        {
            var segment = column.Reader.Segment;

            var result = Segments.Evaluate(segment);

            if (!result.IsEliminated)
            {
                continue;
            }

            await EmitAsync(new AccessStep.SegmentSkipped(rowGroup.RowGroupId,
                                                          segment.Column?.ColumnStoreColumnId ?? -1,
                                                          column.Column.Name,
                                                          result.Reason),
                            cancellationToken);

            await EmitAsync(new AccessStep.RowGroupSkipped(rowGroup.RowGroupId, result.Reason), cancellationToken);

            return true;
        }

        return false;
    }

    private List<ScanColumn> Bind(RowGroupReader reader, RowGroup rowGroup)
    {
        var columns = new List<ScanColumn>();

        foreach (var columnId in Definition.ColumnIds)
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

            columns.Add(new ScanColumn(segmentReader, column, hasDictionary));
        }

        return columns;
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

    private sealed record ScanColumn(SegmentReader Reader, BatchColumn Column, bool HasDictionary);
}
