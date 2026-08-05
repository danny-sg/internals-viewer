namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record ConcatenationDefinition(IReadOnlyList<IteratorDefinition> Inputs) : IteratorDefinition;
