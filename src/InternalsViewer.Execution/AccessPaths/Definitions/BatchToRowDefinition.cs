namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record BatchToRowDefinition(IteratorDefinition Batch) : UnaryDefinition(Batch);
