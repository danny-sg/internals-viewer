using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces.Services.Joins;
using InternalsViewer.Execution.Interfaces.Services.Joins.Inputs;
using InternalsViewer.Execution.Services.Joins.Inputs;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.Services.Joins;

/// <summary>
/// Base for a join, holding the two inputs and the step history every join keeps
/// </summary>
public abstract class JoinStepService : IJoinStepService
{
    public const int OuterSource = 0;

    public const int InnerSource = 1;

    public const int JoinSource = -1;

    public IReadOnlyList<AccessStep> History => TakenSteps;

    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    public bool IsComplete { get; protected set; }

    public int PairCount { get; protected set; }

    public JoinType JoinType { get; protected set; } = JoinType.Inner;

    public JoinInput Outer { get; protected set; } = null!;

    public JoinInput Inner { get; protected set; } = null!;

    public AccessStrategy? Strategy => Outer.Strategy;

    IJoinInput IJoinStepService.Outer => Outer;

    IJoinInput IJoinStepService.Inner => Inner;

    public abstract PageAddress? CurrentPageAddress { get; }

    protected List<AccessStep> TakenSteps { get; } = [];

    public abstract Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clears the state a previous run left behind, before the inputs are started
    /// </summary>
    protected void ResetJoin(JoinType joinType)
    {
        JoinType = joinType;
        PairCount = 0;
        IsComplete = false;

        TakenSteps.Clear();
        Outer.Clear();
        Inner.Clear();
    }

    protected AccessStep Take(AccessStep step, int source, AccessCounters counters)
    {
        var taken = step with { Source = source, Counters = counters };

        TakenSteps.Add(taken);

        return taken;
    }
}
