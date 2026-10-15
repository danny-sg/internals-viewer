using InternalsViewer.Query.FullTrace.Batches;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class BatchSlotRow(BatchLineage lineage, int rowIndex)
{
    public BatchLineage Lineage { get; } = lineage;

    public BatchCapture Capture => Lineage.Capture;

    public int RowIndex { get; } = rowIndex;

    public bool IsSelected => Lineage.IsSelected(RowIndex);

    public bool IsRemoved => Lineage.IsRemoved(RowIndex);

    public int Call => Capture.Call;

    public int CallNumber => Capture.Call + 1;
}
