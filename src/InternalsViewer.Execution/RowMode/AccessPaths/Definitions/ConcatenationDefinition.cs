namespace InternalsViewer.Execution.RowMode.AccessPaths.Definitions;

public sealed record ConcatenationDefinition(IReadOnlyList<IteratorDefinition> Inputs) : IteratorDefinition;
