using InternalsViewer.Execution.Common.AccessPaths.Predicates;
using InternalsViewer.Execution.BatchMode.Data.Vectors;
using InternalsViewer.Execution.Common.Iterators.Common;

namespace InternalsViewer.Execution.BatchMode.Iterators.DataAccess;

/// <summary>
/// Folds a columnstore scan's batches straight into a Hash Aggregate's local table when the aggregate is pushed into the scan
/// </summary>
/// <remarks>
/// Aggregate Pushdown is where aggregations are executed inside the columnstore scan directly.
///
/// The scan reports no batches and no rows. What it folds is counted as locally aggregated instead, which is why a pushed down scan shows
/// ActualRows and Batches of zero next to an ActualLocallyAggregatedRows of the whole table. SQL Server still emits batches, each carrying
/// grouping keys and a partial aggregate rather than rows, they are simply not counted as batches. Here the values go straight into the
/// sink, so that intermediate form is skipped.
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
/// The sink builder is the Hash Aggregate's local table, set on the scan so the current batch can be added to it. The global table stays
/// with the Hash Aggregate and takes these partials at its merge, which is what RecomputeHashForGlobalAggregation does above.
/// </remarks>
internal sealed class ColumnstoreScanAggregatePushdown
{
    public bool IsActive => Sink is not null;

    public long LocallyAggregatedRows { get; private set; }

    private HashAggregateBuilder? Sink { get; set; }

    private EvaluationContext? Context { get; set; }

    public void SetSink(HashAggregateBuilder sink, EvaluationContext context)
    {
        Sink = sink;

        Context = context;
    }

    public (int Rows, long TotalGroups, long NewGroups, bool RunFolded) Accumulate(ExecutionBatch batch)
    {
        var sink = Sink!;

        var groupsBefore = sink.GroupCount;

        var runFolded = AccumulateBatch(batch, sink);

        var rows = batch.SelectionVector.RowCount;

        LocallyAggregatedRows += rows;

        return (rows, sink.GroupCount, sink.GroupCount - groupsBefore, runFolded);
    }

    private bool AccumulateBatch(ExecutionBatch batch, HashAggregateBuilder sink)
    {
        var selection = batch.SelectionVector;

        if (CanFoldRun(batch, sink))
        {
            sink.AccumulateRun(BatchRecordBuilder.Build(batch, selection[0]),
                               Context!,
                               selection.RowCount,
                               null,
                               0);

            return true;
        }

        for (var index = 0; index < selection.RowCount; index++)
        {
            sink.Accumulate(BatchRecordBuilder.Build(batch, selection[index]), Context!);
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

            if (vector is not { IsPure: true })
            {
                return false;
            }
        }

        return true;
    }
}
