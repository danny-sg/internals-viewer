namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record BatchFilterDefinition(IteratorDefinition Source) : UnaryDefinition(Source), IBatchDefinition;
