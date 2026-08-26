namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record BatchToRowDefinition(ColumnstoreScanDefinition Batch) : UnaryDefinition(Batch);
