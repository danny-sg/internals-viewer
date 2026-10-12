namespace InternalsViewer.Query.Export;

internal sealed record ExportResultSet(int RowCount, IReadOnlyList<ExportColumn> Columns);
