using System.Data;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Vectors;

namespace InternalsViewer.Execution.Iterators.BatchMode.DataAccess;

/// <summary>
/// Factory for the creation of the Execution Batch for a columnstore scan
/// </summary>
/// <remarks>
/// Sizes it and builds the scan's own and pipeline vectors.
/// 
/// The scan owns a vector per projected column and a passthrough vector per pipeline column. The row count is sized from the total column
/// count so wider batches carry fewer rows. The scan's own vectors are handed back so the iterator can expose them as its output and bind
/// them per rowgroup.
/// </remarks>
internal static class ColumnstoreScanBatchFactory
{
    public static (ExecutionBatch Batch, List<BatchVector> OwnVectors) Create(IReadOnlyList<string> columnNames,
                                                                              IReadOnlyList<string> pipelineColumnNames)
    {
        var batchRows = BatchSize.GetRowCount(columnNames.Count + pipelineColumnNames.Count);

        var ownVectors = new List<BatchVector>(columnNames.Count);

        var vectors = new List<BatchVector>(columnNames.Count + pipelineColumnNames.Count);

        foreach (var name in columnNames)
        {
            var vector = new BatchVector(new BatchColumn { Name = name }, batchRows);

            ownVectors.Add(vector);

            vectors.Add(vector);
        }

        foreach (var name in pipelineColumnNames)
        {
            vectors.Add(new BatchVector(new BatchColumn { Name = name, DataType = SqlDbType.Variant }, batchRows));
        }

        return (new ExecutionBatch(batchRows, vectors, new BatchDeepDataStore()), ownVectors);
    }
}
