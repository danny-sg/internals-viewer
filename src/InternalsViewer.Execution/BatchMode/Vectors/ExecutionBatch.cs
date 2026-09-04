using InternalsViewer.Execution.Interfaces.BatchMode;

namespace InternalsViewer.Execution.BatchMode.Vectors;

/// <summary>
/// Representation of a Batch mode Batch
/// </summary>
public sealed class ExecutionBatch(int capacity, IReadOnlyList<BatchVector> vectors, IDeepDataContext deepDataContext)
{
    public int Capacity { get; } = capacity;

    public int RowCount { get; private set; } = capacity;

    public IReadOnlyList<BatchVector> Vectors => VectorList;

    public IDeepDataContext DeepDataContext { get; } = deepDataContext;

    public SelectionVector SelectionVector { get; } = new(capacity);

    public int RowGroupId { get; set; }

    public int PureVectorCount => VectorList.Count(v => v.IsPure);

    private List<BatchVector> VectorList { get; } = [.. vectors];

    public void Reset(int rowCount)
    {
        DeepDataContext.Clear();

        foreach (var vector in VectorList)
        {
            vector.ClearPure();
        }

        SetRowCount(rowCount);
    }

    public void SetRowCount(int rowCount)
    {
        RowCount = rowCount;

        SelectionVector.Reset(rowCount);
    }

    public void AddVector(BatchVector vector) => VectorList.Add(vector);

    public BatchVector? FindVector(string columnName)
        => VectorList.Find(v => string.Equals(v.Column.Name, columnName, StringComparison.OrdinalIgnoreCase));
}
