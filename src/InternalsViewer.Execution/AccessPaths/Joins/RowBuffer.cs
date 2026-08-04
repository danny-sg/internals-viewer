namespace InternalsViewer.Execution.AccessPaths.Joins;

public sealed record RowBuffer(string Name, int InputIndex, IReadOnlyList<JoinBufferRow> Rows);
