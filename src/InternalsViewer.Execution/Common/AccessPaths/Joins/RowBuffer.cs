namespace InternalsViewer.Execution.Common.AccessPaths.Joins;

public sealed record RowBuffer(string Name, int InputIndex, IReadOnlyList<JoinBufferRow> Rows);
