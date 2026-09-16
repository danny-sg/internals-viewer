using System.Data.SqlTypes;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Internals.Columnstore.Decoding;

namespace InternalsViewer.Execution.Iterators.BatchMode.DataAccess;

/// <summary>
/// Materialises a batch column vectors from the rowgroup readers and compacts the surviving rows
/// </summary>
internal static class ColumnstoreScanProjector
{
    /// <summary>
    /// Fills each bound vector with the values for the batch's rows, returning the number of values materialised
    /// </summary>
    public static int Fill(IReadOnlyList<ScanColumn> columns,
                           IReadOnlyList<BatchVector> vectors,
                           int fromRow,
                           ReadOnlySpan<bool> mask,
                           IDeepDataContext deepData)
    {
        var materialised = 0;

        for (var i = 0; i < columns.Count; i++)
        {
            FillVector(columns[i], vectors[i], fromRow, mask, deepData, ref materialised);
        }

        return materialised;
    }

    /// <summary>
    /// Compacts the surviving rows of each impure vector into a dense run and sets the batch selection vector
    /// </summary>
    /// <remarks>
    /// With compaction the selection vector is set to 0 - Row Count - 1.
    /// </remarks>
    public static void Compact(ExecutionBatch batch, IReadOnlyList<BatchVector> vectors, ReadOnlySpan<bool> rowMask)
    {
        var compactedRow = 0;

        for (var row = 0; row < rowMask.Length; row++)
        {
            if (!rowMask[row])
            {
                continue;
            }

            if (compactedRow != row)
            {
                foreach (var vector in vectors)
                {
                    if (!vector.IsPure)
                    {
                        vector.Values[compactedRow] = vector.Values[row];
                    }
                }
            }

            compactedRow++;
        }

        batch.SelectionVector.Reset(compactedRow);
    }

    /// <summary>
    /// Fills a vector with values for a column
    /// </summary>
    /// <remarks>
    /// The fill has two paths:
    ///
    /// 1. Pure - Optimized RLE path
    ///    - If all values are all the same and no rows are masked same the vector is set as a constant value with no fill
    ///    - Else all values are filled with the single materialized value
    ///
    /// 2. Impure - Non-RLE path - value is constructed per row
    /// </remarks>
    private static void FillVector(ScanColumn column,
                                   BatchVector vector,
                                   int fromRow,
                                   ReadOnlySpan<bool> rowMask,
                                   IDeepDataContext deepDataContext,
                                   ref int materialisedCount)
    {
        foreach (var run in column.Reader.DataIds.GetRuns(fromRow, rowMask.Length))
        {
            var offset = run.FirstRow - fromRow;

            // Pure
            if (run.Origin == SegmentValueOrigin.RleRun)
            {
                if (rowMask.Slice(offset, run.RowCount).IndexOf(true) < 0)
                {
                    continue;
                }

                var batchValue = CreateBatchValue(column, run.Value, run.FirstRow, deepDataContext);

                materialisedCount++;

                if (offset == 0 && run.RowCount == rowMask.Length)
                {
                    vector.SetPureValue(batchValue);

                    continue;
                }

                vector.Values.AsSpan(offset, run.RowCount).Fill(batchValue);

                continue;
            }

            // Impure
            for (var i = 0; i < run.RowCount; i++)
            {
                if (!rowMask[offset + i])
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

    /// <summary>
    /// Normalize a value + store in deep data if required
    /// </summary>
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

    private static byte[] ToDeepBytes(object? value) => value switch
    {
        byte[] bytes 
            => bytes,
        long number 
            => BitConverter.GetBytes(number),
        double number 
            => BitConverter.GetBytes(number),
        SqlDecimal number 
            => number.BinData,
        _ => []
    };
}
