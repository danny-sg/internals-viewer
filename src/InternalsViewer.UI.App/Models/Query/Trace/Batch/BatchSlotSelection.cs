namespace InternalsViewer.UI.App.Models.Query.Trace.Batch;

/// <summary>
/// The cell a click landed on, being a row and the vector it was read from
/// </summary>
public readonly record struct BatchSlotSelection(int RowIndex, int Ordinal);
