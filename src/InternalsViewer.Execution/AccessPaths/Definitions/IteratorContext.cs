using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// What every iterator in a tree shares while it runs, as opposed to the definition that describes one operator
/// </summary>
public sealed record IteratorContext(DatabaseSource Database)
{
    public EvaluationContext EvaluationContext { get; init; } = EvaluationContext.Now;

    /// <summary>
    /// Totals to carry on from, when an iterator is reopened part way through a walk rather than started fresh
    /// </summary>
    public AccessCounters Counters { get; init; }
}
