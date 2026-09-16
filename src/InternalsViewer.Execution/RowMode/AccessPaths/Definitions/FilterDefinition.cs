namespace InternalsViewer.Execution.RowMode.AccessPaths.Definitions;

public sealed record FilterDefinition(IteratorDefinition Source) : UnaryDefinition(Source);
