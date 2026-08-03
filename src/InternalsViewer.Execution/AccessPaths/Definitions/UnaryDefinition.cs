namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes an operator that reads one input and passes its rows on
/// </summary>
/// <remarks>
/// An operator like this shows nothing of a table itself, so anything walking the tree for the objects it reads goes straight through to
/// <see cref="Source"/>.
/// </remarks>
public abstract record UnaryDefinition(IteratorDefinition Source) : IteratorDefinition;
