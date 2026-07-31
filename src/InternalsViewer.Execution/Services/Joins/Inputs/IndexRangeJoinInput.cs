using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Execution.Services.Joins.Inputs;

/// <summary>
/// An input that walks an index in key order, read straight through rather than restarted
/// </summary>
public sealed class IndexRangeJoinInput(IndexStepService service, RangeDefinition definition) : JoinInput
{
    public override IStepService Service => service;

    public override AccessStrategy? Strategy => service.Strategy;

    public override string StartDescription => "in key order";

    public Task StartAsync(DatabaseSource database, CancellationToken cancellationToken, EvaluationContext? evaluationContext = null)
        => service.StartAsync(database,
                              definition.AllocationUnitId,
                              definition.RootPage,
                              definition.Ranges,
                              definition.Residual,
                              definition.Direction,
                              cancellationToken,
                              definition.RowGoal,
                              definition.HasUntranslatedResidual,
                              evaluationContext);
}
