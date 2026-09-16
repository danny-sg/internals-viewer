namespace InternalsViewer.Execution.BatchMode.AccessPaths.Definitions;

public sealed record BatchFilterDefinition(IteratorDefinition Source) : UnaryDefinition(Source), IBatchDefinition;
