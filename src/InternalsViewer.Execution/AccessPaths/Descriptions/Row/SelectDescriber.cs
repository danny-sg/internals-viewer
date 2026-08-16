using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Row;

public static class SelectDescriber
{
    public static OperatorDescription Describe()
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Open,
            Title = "Open",
            Lead = "Open child operators that will cascade through the plan operator tree."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.GetRow,
            Title = "Get Row",
            Lead = "Row request to child operators that will cascade through the plan operator tree."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "Close child operators that will cascade through the plan operator tree."
        });

        return new OperatorDescription
        {
            Summary = "Results projection to return rows from the operators.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
