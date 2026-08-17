using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Row;

public static class FilterDescriber
{
    public static OperatorDescription Describe(FilterDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Filter,
            Title = "Filter",
            Lead = "Each row arriving from the input is tested and either passed up or dropped. Nothing is held and nothing is "
                   + "reordered, so a row that passes is returned before the next one is read.",
            Condition = PhaseCondition.Of(definition.Residual)
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends when its input runs out. A filter never stops the input early, because it cannot know that no "
                   + "later row will pass."
        });

        return new OperatorDescription
        {
            Summary = "Operator that tests every row against a predicate and passes on only those that match, which is where a "
                      + "predicate lands when it cannot be pushed into the access path below.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
