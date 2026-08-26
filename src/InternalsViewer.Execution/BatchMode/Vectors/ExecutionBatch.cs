using InternalsViewer.Execution.Interfaces.BatchMode;

namespace InternalsViewer.Execution.BatchMode.Vectors;

public sealed class ExecutionBatch(int rowCount, IReadOnlyList<BatchVector> vectors, IDeepDataContext deepData)
{
    public int RowCount { get; } = rowCount;

    public IReadOnlyList<BatchVector> Vectors { get; } = vectors;

    public IDeepDataContext DeepData { get; } = deepData;

    public SelectionBitmap SelectionBitmap { get; } = new(rowCount);

    public int RowGroupId { get; init; }

    public BatchVector? Find(string columnName)
        => Vectors.FirstOrDefault(v => string.Equals(v.Column.Name, columnName, StringComparison.OrdinalIgnoreCase));
}
