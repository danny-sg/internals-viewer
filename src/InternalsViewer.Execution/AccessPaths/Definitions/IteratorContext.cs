using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// What every iterator in a tree shares while it runs, as opposed to the definition that describes one operator
/// </summary>
public sealed record IteratorContext(DatabaseSource Database)
{
    public EvaluationContext EvaluationContext { get; init; } = EvaluationContext.Now;

    public IStepSink Steps { get; init; } = NullStepSink.Instance;

    public IRecord? CorrelatedRow { get; init; }
}
