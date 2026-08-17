namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record FilterDefinition(IteratorDefinition Source) : UnaryDefinition(Source);
