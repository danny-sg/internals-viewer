namespace InternalsViewer.Execution.RowMode.AccessPaths.Definitions;

public sealed record SelectDefinition(IteratorDefinition Source) : UnaryDefinition(Source);
