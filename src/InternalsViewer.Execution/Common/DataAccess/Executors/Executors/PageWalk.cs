using InternalsViewer.Execution.Common.AccessPaths.Predicates;
using InternalsViewer.Execution.Common.AccessPaths.Results;
using InternalsViewer.Execution.Common.Interfaces.Pages;

namespace InternalsViewer.Execution.Common.DataAccess.Executors.Executors;

/// <summary>
/// Executor page walk definition
/// </summary>
/// <remarks>
/// Defines how to read a page rows, including residual predicate and row goal.
/// </remarks>
internal record PageWalk
{
    public AccessPredicate? Residual { get; init; }

    public long? RowGoal { get; init; }

    public AccessCounters Counters { get; init; }

    public EvaluationContext EvaluationContext { get; init; } = EvaluationContext.Now;

    public bool HasResidual => Residual is not (null or AccessPredicate.True or AccessPredicate.NoTranslation);

    public bool? Evaluate(IRowPageAccessor page, int slot)
        => HasResidual ? PredicateEvaluator.Evaluate(Residual!, page.BindRow(slot), EvaluationContext) : true;
}
