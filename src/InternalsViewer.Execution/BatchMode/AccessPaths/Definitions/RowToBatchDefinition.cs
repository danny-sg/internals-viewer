namespace InternalsViewer.Execution.BatchMode.AccessPaths.Definitions;

public sealed record RowToBatchDefinition(IteratorDefinition Row) : UnaryDefinition(Row), IBatchDefinition;
