using InternalsViewer.Execution.BatchMode.Normalization;

namespace InternalsViewer.UI.App.Models.Query.Trace.Batch;

/// <summary>
/// One row of the batch, holding the slot each vector gave it
/// </summary>
public sealed class BatchRowView
{
    public required int RowIndex { get; set; }

    public required bool IsSelected { get; set; }

    public required BatchValue[] Values { get; set; }
}
