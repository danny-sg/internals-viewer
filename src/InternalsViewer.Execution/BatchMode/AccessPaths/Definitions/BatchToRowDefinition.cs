namespace InternalsViewer.Execution.BatchMode.AccessPaths.Definitions;

public sealed record BatchToRowDefinition(IteratorDefinition Batch) : UnaryDefinition(Batch);
