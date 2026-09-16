using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Elimination;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Services;

namespace InternalsViewer.Execution.Iterators.BatchMode.DataAccess;

/// <summary>
/// Iterates a columnstore index's compressed rowgroups, yielding the next that survives elimination with its columns bound
/// </summary>
internal sealed class ColumnstoreScanRowGroupCursor(ColumnstoreService columnstoreService,
                                                    ColumnstoreScanDefinition definition,
                                                    IteratorContext context,
                                                    int nodeId,
                                                    IReadOnlyList<int> columnIds,
                                                    IReadOnlyList<RowGroup> rowGroups)
{
    private readonly PartitionEliminator _partitions = new(definition.Residual);

    private readonly SegmentEliminator _segments = new(definition.Residual);

    private readonly HashSet<long> _skippedPartitions = [];

    private readonly HashSet<(long HobtId, int ColumnId, int DictionaryId)> _openDictionaries = [];

    private int _index;

    /// <summary>
    /// Advances to the next rowgroup that survives elimination with its columns bound, or null when none remain
    /// </summary>
    /// <remarks>
    /// Elimination is run per rowgroup. If a rowgroup is eliminated it keeps iterating until either a non-eliminated rowgroup is found or
    /// the rowgroups are exhausted. Dictionary based compressed data filters can also eliminate the rowgroup - if no values match against
    /// the dictionary the rowgroup is eliminated.
    /// </remarks>
    public async Task<BoundRowGroup?> NextAsync(CancellationToken cancellationToken)
    {
        while (_index < rowGroups.Count)
        {
            var rowGroup = rowGroups[_index++];

            if (await IsRowGroupEliminatedAsync(rowGroup, cancellationToken))
            {
                continue;
            }

            var reader = await OpenRowGroupAsync(rowGroup, cancellationToken);

            var columns = BindColumns(reader, rowGroup);

            if (columnIds.Count > 0 && (columns.Count == 0 || reader.RowCount == 0))
            {
                await EmitAsync(new AccessStep.RowGroupSkipped(rowGroup.RowGroupId, "No readable segments"), cancellationToken);

                continue;
            }

            if (FindEliminatedDictionaryReader(reader) is { } unmatched)
            {
                await EliminateOnDictionaryAsync(rowGroup, reader, unmatched, cancellationToken);

                continue;
            }

            return new BoundRowGroup(rowGroup, reader, columns);
        }

        return null;
    }

    /// <summary>
    /// Returns the row bucket size for the next batch - the shortest leading RLE run across the columns
    /// </summary>
    /// <remarks>
    /// A batch never spans a run boundary, so the next batch is capped at the shortest leading run across the columns. This keeps each
    /// column's leading run pure for the batch, which is what lets a pure vector be read straight from the run.
    /// </remarks>
    public static int GetRowBucketSize(IReadOnlyList<ScanColumn> columns, int fromRow, int max)
    {
        var limit = max;

        foreach (var column in columns)
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

    private ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken)
        => context.Steps.EmitAsync(step with { NodeId = nodeId }, cancellationToken);

    private async Task<bool> IsRowGroupEliminatedAsync(RowGroup rowGroup, CancellationToken cancellationToken)
    {
        var partition = _partitions.Evaluate(rowGroup);

        if (partition.IsEliminated)
        {
            if (_skippedPartitions.Add(rowGroup.PartitionId))
            {
                await EmitAsync(new AccessStep.PartitionSkipped(rowGroup.PartitionId, partition.Reason), cancellationToken);
            }

            return true;
        }

        return await IsEliminatedAsync(rowGroup, cancellationToken);
    }

    private async Task<bool> IsEliminatedAsync(RowGroup rowGroup, CancellationToken cancellationToken)
    {
        var projected = rowGroup.Segments
                                .Where(s => s.Column is not null && columnIds.Contains(s.Column.ColumnStoreColumnId))
                                .ToList();

        var eliminated = new List<string>();

        foreach (var segment in projected)
        {
            var result = _segments.Evaluate(segment);

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

        await EmitAsync(new AccessStep.RowGroupSkipped(rowGroup.RowGroupId, message), cancellationToken);

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

    /// <summary>
    /// Opens a rowgroup
    /// </summary>
    private async Task<RowGroupReader> OpenRowGroupAsync(RowGroup rowGroup, CancellationToken cancellationToken)
    {
        var requiredSegments = columnIds.Count == 0
                     ? []
                     : rowGroup.Segments
                               .Where(s => s.Column is null || columnIds.Contains(s.Column.ColumnStoreColumnId))
                               .ToList();

        var readers = new List<SegmentReader>();

        var skipped = new List<ColumnSegment>();

        for (var i = 0; i < requiredSegments.Count; i++)
        {
            var segment = requiredSegments[i];

            foreach (var dictionary in GetSegmentDictionaries(segment))
            {
                if (dictionary.IsGlobal
                    && !_openDictionaries.Add((dictionary.HobtId, dictionary.ColumnId, dictionary.DictionaryId)))
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
                readers.Add(await columnstoreService.GetSegmentReader(context.Database, segment, cancellationToken));
            }
            catch (InvalidDataException)
            {
                skipped.Add(segment);
            }
        }

        return new RowGroupReader(rowGroup, readers, skipped);
    }

    /// <summary>
    /// Find and return the first Segment Dictionary Reader where the segment can't match anything in the column dictionary
    /// </summary>
    private SegmentReader? FindEliminatedDictionaryReader(RowGroupReader reader)
    {
        if (definition.Residual is not { } residual)
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

            if (CompressedDataFilter.GetMatchingDictionaryIds(residual, segmentReader, context.EvaluationContext) is { Count: 0 })
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

    /// <summary>
    /// Binds Scan Columns to the RowGroup and Reader
    /// </summary>
    private List<ScanColumn> BindColumns(RowGroupReader reader, RowGroup rowGroup)
    {
        var columns = new List<ScanColumn>();

        foreach (var columnId in columnIds)
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

            var filter = definition.IsFilterOnCompressedDataUsed
                            ? CompressedDataFilter.Create(definition.Residual, segmentReader, context.EvaluationContext)
                            : null;

            columns.Add(new ScanColumn(segmentReader, column, hasDictionary, filter));
        }

        return columns;
    }
}