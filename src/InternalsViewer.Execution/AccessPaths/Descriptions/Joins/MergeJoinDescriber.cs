using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Joins;

public static class MergeJoinDescriber
{
    public static OperatorDescription Describe(MergeJoinDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Order,
            Title = "Order",
            Lead = "Both inputs have to arrive sorted on the join keys in the same direction, which is what lets each side be read once. " +
                   "One row is taken from each side to start"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Compare,
            Title = "Compare",
            Lead = "Compare the current key on each side and advance the lower one. A side that is behind is skipped over without any of " +
                   "its rows reaching the output"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Match,
            Title = "Match",
            Lead = PhaseCondition.Exists(definition.Residual)
                ? "Equal keys emit a pair where "
                : "Equal keys emit a pair",
            Condition = PhaseCondition.Of(definition.Residual),
            Trail = ". Where the inner side can repeat a key the matching run is held, so it can be replayed against every outer row " +
                    "carrying that key"
        });

        if (definition.JoinType is not JoinType.Inner)
        {
            phases.Add(new AccessStrategyPhase
            {
                Phase = AccessPhase.Preserve,
                Title = "Preserve",
                Lead = "A preserved side emits its unmatched rows with NULLs as the walk passes them, rather than at the end"
            });
        }

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The join ends when either input runs out, except that a preserved side is drained to its end first"
        });

        return new OperatorDescription
        {
            Summary = "Join that reads both inputs once in the same key order, walking them together and advancing whichever side is behind",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
