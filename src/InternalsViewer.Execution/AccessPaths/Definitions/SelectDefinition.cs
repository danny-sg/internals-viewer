namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record SelectDefinition(IteratorDefinition Source) : UnaryDefinition(Source);
