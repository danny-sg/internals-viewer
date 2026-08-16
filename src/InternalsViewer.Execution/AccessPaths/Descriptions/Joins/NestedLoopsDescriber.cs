using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Joins;

public static class NestedLoopsDescriber
{
    public static OperatorDescription Describe(NestedLoopsDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Outer,
            Title = "Outer",
            Lead = "Ask the outer input for its next row. The outer side is read once, start to end, and nothing is held from it."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Rebind,
            Title = "Rebind",
            Lead = "Copy the join columns out of the outer row and reopen the inner input, so the inner side starts a fresh descent for " +
                   "those values."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Inner,
            Title = "Inner",
            Lead = PhaseCondition.Exists(definition.Residual)
                ? "Drain the inner input for that one bound value, pairing each row it returns with the outer row that bound it where "
                : "Drain the inner input for that one bound value. Every row it returns is paired with the outer row that bound it.",
            Condition = PhaseCondition.Of(definition.Residual),
            Trail = PhaseCondition.Exists(definition.Residual) ? "." : string.Empty
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Verdict,
            Title = "Verdict",
            Lead = "Weigh what the rebind returned against the join type. An inner join drops an outer row that matched nothing, an " +
                   "outer join emits it once with NULLs, and a semi join emits it at the first match and closes the inner early."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The join ends when the outer input runs out, by which time the inner input has been opened once per outer row."
        });

        return new OperatorDescription
        {
            Summary = "Join that reads its outer input once and re-runs (rebinds) the inner input from the start for every outer row, " +
                      "with the outer row's values bound into the inner side.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
