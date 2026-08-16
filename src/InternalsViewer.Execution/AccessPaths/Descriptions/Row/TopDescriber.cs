using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Row;

public static class TopDescriber
{
    public static OperatorDescription Describe(TopDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.RowCount,
            Title = "Row Count",
            Lead = $"The count of {definition.RowCount:N0} rows is fixed when the operator opens, and is pushed down as a row goal so the " +
                   "access path below can stop on its own"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Pass,
            Title = "Pass",
            Lead = "Each row arriving from the input is counted and passed up unchanged, with nothing held and nothing reordered"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Stop,
            Title = "Stop",
            Lead = "Reaching the count closes the input mid-walk. What ends the scan below is this close, not the end of the data"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends at the limit, or earlier when the input runs out with fewer rows"
        });

        return new OperatorDescription
        {
            Summary = $"Operator that passes rows through, counting them, and stops asking its input once {definition.RowCount:N0} rows " +
                      "have been returned",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
