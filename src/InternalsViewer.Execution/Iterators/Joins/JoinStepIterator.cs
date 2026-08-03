using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Interfaces.Iterators.Joins.Inputs;
using InternalsViewer.Execution.Iterators.Joins.Inputs;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.Iterators.Joins;

/// <summary>
/// Base for a join, holding the two inputs and the step history every join keeps
/// </summary>
public abstract class JoinStepIterator : IJoinStepIterator
{
    public const int OuterSource = 0;

    public const int InnerSource = 1;

    public const int JoinSource = -1;

    public int IteratorId { get; set; } = JoinSource;

    public int PairCount { get; protected set; }

    public JoinInput Outer { get; protected set; } = null!;

    public JoinInput Inner { get; protected set; } = null!;

    public IReadOnlyList<AccessStep> History => TakenSteps;

    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    public bool IsComplete { get; protected set; }

    public JoinType JoinType { get; protected set; } = JoinType.Inner;

    public AccessStrategy? Strategy => Outer.Strategy;

    public abstract PageAddress? CurrentPageAddress { get; }

    IJoinInput IJoinStepIterator.Outer => Outer;

    IJoinInput IJoinStepIterator.Inner => Inner;

    /// <summary>
    /// The token the walk in progress is running under, which an input pulls rows with
    /// </summary>
    internal CancellationToken CurrentToken { get; set; }

    protected int OuterIteratorId { get; private set; } = OuterSource;

    protected int InnerIteratorId { get; private set; } = InnerSource;

    protected List<AccessStep> TakenSteps { get; } = [];

    public abstract Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken);

    public abstract Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Closes the join and both of its inputs
    /// </summary>
    public virtual async Task CloseAsync()
    {
        IsComplete = true;

        await Outer.Iterator.CloseAsync();
        await Inner.Iterator.CloseAsync();
    }

    /// <summary>
    /// Gives each input its own identity, so the steps it produces can be told apart from this join's
    /// </summary>
    /// <remarks>
    /// The defaults only stay unique while a join reads two leaves. Composing joins needs ids unique across the whole tree, which the
    /// caller supplies from the plan.
    /// </remarks>
    public void AssignIteratorIds(int outerIteratorId, int innerIteratorId, int joinIteratorId)
    {
        OuterIteratorId = outerIteratorId;
        InnerIteratorId = innerIteratorId;
        IteratorId = joinIteratorId;
    }

    /// <summary>
    /// Records what an input's step did to this join's counters, leaving the step's own identity alone
    /// </summary>
    internal abstract AccessStep Observe(AccessStep step, int side);

    /// <summary>
    /// Stamps a step this join produced, or passes on one an input produced with only the counters replaced
    /// </summary>
    /// <remarks>
    /// A step already carries the id of whatever produced it, which for a nested operator is somewhere further down. Overwriting that here
    /// would attribute a leaf's reads to whichever input of this join they happened to arrive through.
    /// </remarks>
    protected AccessStep Attribute(AccessStep step, int source, AccessCounters counters)
    {
        return source == JoinSource
            ? step with { Source = IteratorId, Counters = counters }
            : step with { Counters = counters };
    }

    protected void ApplyInputIteratorIds()
    {
        Outer.Iterator.IteratorId = OuterIteratorId;
        Inner.Iterator.IteratorId = InnerIteratorId;
    }

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

        ApplyInputIteratorIds();
    }

    protected AccessStep Take(AccessStep step, int source, AccessCounters counters)
    {
        var taken = Attribute(step, source, counters);

        TakenSteps.Add(taken);

        return taken;
    }
}
