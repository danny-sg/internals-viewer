using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.Execution.AccessPaths.Descriptions;

/// <summary>
/// Works out which described phase a step belongs to, so the phase an operator is in can be lit as it runs
/// </summary>
/// <remarks>
/// A step carries the phase an access path is in, which is the only reading that fits every operator emitting it: a join's emit and a
/// sort's collect are both a walk. The operator the step came from is what tells the two apart, so the definition decides here rather
/// than the step saying it outright.
/// </remarks>
public static class OperatorPhases
{
    public static AccessPhase? Resolve(IteratorDefinition definition, AccessStep step, bool isOwnStep)
    {
        if (!isOwnStep)
        {
            return definition is NestedLoopsDefinition && step is AccessStep.Rebind ? AccessPhase.Rebind : null;
        }

        return definition switch
        {
            NestedLoopsDefinition => NestedLoops(step),
            MergeJoinDefinition => MergeJoin(step),
            HashMatchDefinition => HashMatch(step),
            TopDefinition => Top(step),
            SortDefinition => Sort(step),
            StreamAggregateDefinition => StreamAggregate(step),
            HashAggregateDefinition => HashAggregate(step),
            ComputeScalarDefinition => ComputeScalar(step),
            ConcatenationDefinition => Concatenation(step),
            SelectDefinition => Select(step),
            SeekDefinition => step is AccessStep.Rebind ? AccessPhase.Rebind : step.AccessPhase,
            _ => step.AccessPhase
        };
    }

    private static AccessPhase? NestedLoops(AccessStep step)
        => step switch
        {
            AccessStep.Open or AccessStep.JoinStart => AccessPhase.Outer,
            AccessStep.Rebind => AccessPhase.Rebind,
            AccessStep.JoinVerdict => AccessPhase.Verdict,
            AccessStep.JoinEmit => AccessPhase.Inner,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };

    private static AccessPhase? MergeJoin(AccessStep step)
        => step switch
        {
            AccessStep.Open or AccessStep.JoinStart => AccessPhase.Order,
            AccessStep.MergeCompare { Comparison: 0 } or AccessStep.MergeCompareRun { Comparison: 0 } => AccessPhase.Match,
            AccessStep.MergeCompare or AccessStep.MergeCompareRun => AccessPhase.Compare,
            AccessStep.JoinEmit { IsUnmatched: true } => AccessPhase.Preserve,
            AccessStep.JoinEmit => AccessPhase.Match,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };

    private static AccessPhase? HashMatch(AccessStep step)
        => step switch
        {
            AccessStep.Open => AccessPhase.Buckets,
            AccessStep.HashBuild => AccessPhase.Build,
            AccessStep.HashProbe or AccessStep.HashProbeRun => AccessPhase.Probe,
            AccessStep.HashCompare => AccessPhase.Compare,
            AccessStep.JoinEmit { OuterRecord: not null, InnerRecord: null } => AccessPhase.Complete,
            AccessStep.JoinEmit => AccessPhase.Probe,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };

    private static AccessPhase? Top(AccessStep step)
        => step switch
        {
            AccessStep.Open or AccessStep.TopStart => AccessPhase.RowCount,
            AccessStep.TopRow { IsLast: true } => AccessPhase.Stop,
            AccessStep.TopRow => AccessPhase.Pass,
            AccessStep.Stopped { Reason: StopReason.RowGoalMet } => AccessPhase.Stop,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };

    private static AccessPhase? Sort(AccessStep step)
        => step switch
        {
            AccessStep.Open or AccessStep.SortCollect => AccessPhase.Collect,
            AccessStep.Sorted => AccessPhase.Sort,
            AccessStep.SortRow => AccessPhase.Emit,
            AccessStep.SortDuplicate => AccessPhase.Duplicate,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };

    private static AccessPhase? StreamAggregate(AccessStep step)
        => step switch
        {
            AccessStep.Open or AccessStep.AggregateStart or AccessStep.AggregateGroup => AccessPhase.Group,
            AccessStep.AggregateRow => AccessPhase.Accumulate,
            AccessStep.AggregateEmit => AccessPhase.Emit,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };

    private static AccessPhase? HashAggregate(AccessStep step)
        => step switch
        {
            AccessStep.Open or AccessStep.AggregateStart => AccessPhase.Buckets,
            AccessStep.HashAggregate => AccessPhase.Accumulate,
            AccessStep.AggregateEmit => AccessPhase.Emit,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };

    private static AccessPhase? ComputeScalar(AccessStep step)
        => step switch
        {
            AccessStep.Open or AccessStep.ComputeRow => AccessPhase.Compute,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };

    private static AccessPhase? Concatenation(AccessStep step)
        => step switch
        {
            AccessStep.Open or AccessStep.InputStart => AccessPhase.Inputs,
            AccessStep.ConcatRow => AccessPhase.Pass,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };

    private static AccessPhase? Select(AccessStep step)
        => step switch
        {
            AccessStep.Open => AccessPhase.Open,
            AccessStep.Output => AccessPhase.GetRow,
            AccessStep.Stopped or AccessStep.Close => AccessPhase.Complete,
            _ => null
        };
}
