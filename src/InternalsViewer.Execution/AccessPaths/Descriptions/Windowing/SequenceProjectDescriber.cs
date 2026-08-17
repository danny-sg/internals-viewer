using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Windowing;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Windowing;

public static class SequenceProjectDescriber
{
    public static OperatorDescription Describe(SequenceProjectDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        var functions = string.Join(", ", definition.Columns.Select(c => c.ToText()));

        var isTieAware = definition.Columns.Any(c => c.Function != RankingFunction.RowNumber);

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Rank,
            Title = "Rank",
            Lead = isTieAware
                ? $"Each row arriving from the input carries {functions} worked out from a running count. The Segment below sets a flag "
                  + "on the first row of each partition, which restarts the count, and a second flag when the ordering columns change, "
                  + "which is how rows that tie are given the same value."
                : $"Each row arriving from the input carries {functions} worked out from a running count, restarted whenever the Segment "
                  + "below flags the first row of a partition."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends when its input runs out. Ranking depends only on the rows already seen, so nothing is held back "
                   + "waiting for the partition to finish."
        });

        return new OperatorDescription
        {
            Summary = "Operator that numbers rows within a window, using the flags a Segment set to know where to restart the count.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
