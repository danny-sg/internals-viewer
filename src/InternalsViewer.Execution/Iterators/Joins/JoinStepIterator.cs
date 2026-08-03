using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Interfaces.Iterators.Joins.Inputs;
using InternalsViewer.Execution.Iterators.Joins.Inputs;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Interfaces.Engine;
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

    protected List<AccessStep> TakenSteps { get; } = [];

    public abstract Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// The pair this join emitted, combined into the single row an operator above reads
    /// </summary>
    public IRecord? GetOutputRow(AccessStep step)
        => step.Source == IteratorId && step is AccessStep.JoinEmit emit
            ? JoinedRecord.Combine(emit.OuterRecord, emit.InnerRecord)
            : null;

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

    /// <summary>
    /// Clears the state a previous run left behind, before the inputs are started
    /// </summary>
    /// <remarks>
    /// Input ids are not touched here. Each iterator is stamped once by the factory that built it, from the plan node it runs, and a join
    /// restamping its inputs would collapse every tree onto the same two ids no matter how deep it went.
    /// </remarks>
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
        var taken = Attribute(step, source, counters);

        TakenSteps.Add(taken);

        return taken;
    }
}
