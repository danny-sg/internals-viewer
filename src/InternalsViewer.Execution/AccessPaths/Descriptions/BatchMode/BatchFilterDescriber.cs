using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.BatchMode;

public static class BatchFilterDescriber
{
    public static OperatorDescription Describe(BatchFilterDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Filter,
            Title = "Filter",
            Lead = "The predicate is evaluated across the whole batch first, then the selection vector is rewritten in one pass to hold "
                   + "only the rows that matched. The batch itself is passed on unchanged, so no row is copied and no value is moved.",
            Condition = PhaseCondition.Of(definition.Residual)
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends when its input runs out. A batch left with no selected rows is dropped rather than passed on."
        });

        return new OperatorDescription
        {
            Summary = "Batch mode operator that tests a predicate over a batch and narrows its selection vector, taking the predicates "
                      + "that could not be pushed into the scan below.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
