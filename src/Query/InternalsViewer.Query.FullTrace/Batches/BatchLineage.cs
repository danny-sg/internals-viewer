namespace InternalsViewer.Query.FullTrace.Batches;

public sealed class BatchLineage
{
    public BatchLineage(BatchCapture capture, IReadOnlyList<BatchCapture> inputs)
    {
        Capture = capture;

        Inputs = inputs;

        RowCount = inputs.Select(i => i.RowCount).Prepend(capture.RowCount).Max();

        Selected = [.. capture.Selection];

        InputSelected = inputs.Count > 0 ? [.. inputs[0].Selection] : Selected;
    }

    public BatchCapture Capture { get; }

    public IReadOnlyList<BatchCapture> Inputs { get; }

    public BatchCapture? Input => Inputs.Count > 0 ? Inputs[0] : null;

    public int RowCount { get; }

    private HashSet<int> Selected { get; }

    private HashSet<int> InputSelected { get; }

    public bool IsSelected(int row) => Selected.Contains(row);

    public bool IsRemoved(int row) => !Selected.Contains(row) && InputSelected.Contains(row);

    public (BatchCaptureVector Vector, ulong Slot)? GetSlot(int ordinal, int row)
    {
        if (ordinal >= Capture.Vectors.Count)
        {
            return null;
        }

        var vector = Capture.Vectors[ordinal];

        if (vector.GetSlot(row) is { } slot)
        {
            return (vector, slot);
        }

        foreach (var input in Inputs)
        {
            if (input.Vectors.FirstOrDefault(v => v.VectorId == vector.VectorId) is { } match && match.GetSlot(row) is { } inputSlot)
            {
                return (match, inputSlot);
            }
        }

        return null;
    }
}
