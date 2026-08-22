namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// One row the delete bitmap marks as deleted
/// </summary>
public sealed record DeletedRowSummary(int RowGroupId, long RowId);
