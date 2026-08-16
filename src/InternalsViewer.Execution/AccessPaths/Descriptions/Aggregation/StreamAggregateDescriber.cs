using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Aggregation;

public static class StreamAggregateDescriber
{
    public static OperatorDescription Describe(StreamAggregateDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        var aggregates = string.Join(", ", definition.Aggregates.Select(a => a.ToText()));

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Group,
            Title = "Group",
            Lead = definition.IsScalar
                ? "There is no grouping, so the whole input is one group and a single row is returned even when no rows arrive."
                : $"A group runs for as long as {string.Join(", ", definition.GroupBy)} stays the same. The input arrives in that "
                  + "order, so a change of value is what ends a group and no rows have to be held to find it."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Accumulate,
            Title = "Accumulate",
            Lead = aggregates.Length > 0
                ? $"Each row updates the running totals for {aggregates}. Only the totals are kept, never the rows."
                : "Each row is folded into the running totals, which is all the operator keeps."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Emit,
            Title = "Emit",
            Lead = definition.IsScalar
                ? "The totals are returned as one row once the input runs out."
                : "The totals are returned as one row at the point the group ends, and the row that ended it starts the next group."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends once the input is exhausted and the last group has been returned."
        });

        return new OperatorDescription
        {
            Summary = definition.IsScalar
                ? $"Operator that folds its whole input into a single row of {(aggregates.Length > 0 ? aggregates : "aggregates")}."
                : $"Operator that returns one row per group of {string.Join(", ", definition.GroupBy)}, relying on its input arriving "
                  + "in that order so each group can be returned as soon as it ends.",
            IsStreaming = !definition.IsScalar,
            IsBlocking = definition.IsScalar,
            Phases = phases.ToImmutable()
        };
    }
}
