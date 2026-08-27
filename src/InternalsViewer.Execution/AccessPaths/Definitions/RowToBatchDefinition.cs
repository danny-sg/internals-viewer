namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record RowToBatchDefinition(IteratorDefinition Row) : UnaryDefinition(Row), IBatchDefinition;
