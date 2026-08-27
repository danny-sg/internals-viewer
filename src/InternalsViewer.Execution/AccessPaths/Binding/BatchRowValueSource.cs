using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces.AccessPaths.Binding;

namespace InternalsViewer.Execution.AccessPaths.Binding;

internal sealed class BatchRowValueSource : IRowValueSource
{
    private Dictionary<string, int> VectorsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    private ExecutionBatch? Batch { get; set; }

    private int Row { get; set; }

    public void Bind(ExecutionBatch batch)
    {
        Batch = batch;

        VectorsByName.Clear();

        for (var i = 0; i < batch.Vectors.Count; i++)
        {
            VectorsByName[batch.Vectors[i].Column.Name] = i;
        }
    }

    public void MoveTo(int row) => Row = row;

    public AccessValue GetValue(int ordinal, string? columnName = null)
    {
        if (Batch is not { } batch || columnName is null || !VectorsByName.TryGetValue(columnName, out var index))
        {
            return AccessValue.Null;
        }

        return BatchRecordBuilder.ToValue(batch, batch.Vectors[index], Row);
    }
}
