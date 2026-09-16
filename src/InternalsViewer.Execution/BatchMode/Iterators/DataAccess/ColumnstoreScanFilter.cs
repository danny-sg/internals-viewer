using InternalsViewer.Execution.Common.AccessPaths.Binding;
using InternalsViewer.Execution.Common.AccessPaths.Predicates;
using InternalsViewer.Execution.BatchMode.Data.Vectors;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Execution.BatchMode.Iterators.DataAccess;

/// <summary>
/// Apply filters to a batch row mask
/// </summary>
/// <remarks>
/// The delete bitmap and compressed data filters run on the encoded data ids before the batch is materialised.
///
/// The residual predicate runs on the materialised values after projection.
///
/// The filter has no side effects beyond the mask it is given - it emits no steps. The scan iterator emits the delete and filter steps from
/// the counts returned here.
/// </remarks>
internal static class ColumnstoreScanFilter
{
    /// <summary>
    /// Clears the deleted rows for a rowgroup from the mask, returning how many were cleared
    /// </summary>
    public static int ApplyDeletes(Span<bool> rowMask, DeletedRows deletedRows, int rowGroupId, int from)
    {
        var rows = deletedRows.ForRowGroup(rowGroupId);

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

        for (var i = start; i < rows.Length && rows[i] < from + rowMask.Length; i++)
        {
            rowMask[rows[i] - from] = false;

            cleared++;
        }

        return cleared;
    }

    /// <summary>
    /// Applies each column's compressed data filter to the mask, counting the RLE entries and per-value operations
    /// </summary>
    public static void ApplyCompressed(Span<bool> rowMask,
                                       IReadOnlyList<ScanColumn> columns,
                                       int fromRow,
                                       ref int rleEntryCount,
                                       ref int operationCount)
    {
        foreach (var column in columns)
        {
            if (column.Filter is not { } filter)
            {
                continue;
            }

            if (rowMask.IndexOf(true) < 0)
            {
                break;
            }

            foreach (var run in column.Reader.DataIds.GetRuns(fromRow, rowMask.Length))
            {
                var offset = run.FirstRow - fromRow;

                rleEntryCount++;

                if (run.Origin == SegmentValueOrigin.RleRun)
                {
                    operationCount++;

                    if (filter.IsMatch(run.Value))
                    {
                        continue;
                    }

                    var selected = rowMask.Slice(offset, run.RowCount);

                    selected.Count(true);

                    selected.Clear();

                    continue;
                }

                for (var i = 0; i < run.RowCount; i++)
                {
                    if (!rowMask[offset + i])
                    {
                        continue;
                    }

                    operationCount++;

                    if (filter.IsMatch(column.Reader.DataIds.GetRowDataId(run.FirstRow + i)))
                    {
                        continue;
                    }

                    rowMask[offset + i] = false;
                }
            }
        }
    }

    /// <summary>
    /// Evaluates the residual predicate against the materialised batch values
    /// </summary>
    public static (int Evaluated, int Matches) EvaluateResidual(ExecutionBatch batch,
                                                                Span<bool> rowMask,
                                                                AccessPredicate predicate,
                                                                EvaluationContext context,
                                                                BatchRowValueSource values)
    {
        values.BindBatch(batch);

        var evaluated = 0;

        var matches = 0;

        for (var i = 0; i < rowMask.Length; i++)
        {
            if (!rowMask[i])
            {
                continue;
            }

            evaluated++;

            values.SetRow(i);

            if (PredicateEvaluator.Evaluate(predicate, values, context) == true)
            {
                matches++;

                continue;
            }

            rowMask[i] = false;
        }

        return (evaluated, matches);
    }
}