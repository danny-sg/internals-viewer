using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.BatchMode;

public static class BatchHashAggregateDescriber
{
    public static OperatorDescription Describe(BatchHashAggregateDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        var aggregates = string.Join(", ", definition.Aggregates.Select(a => a.ToText()));

        var groupBy = string.Join(", ", definition.GroupBy);

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Buckets,
            Title = "Buckets",
            Lead = "The table is sized from the estimated number of groups. Too few buckets and groups share a chain that has to be "
                   + "walked on every row, too many and most of the table sits empty."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Accumulate,
            Title = "Accumulate",
            Lead = $"Batches are read from the input and every selected row is hashed on {groupBy} and folded into the group sitting in "
                   + $"that bucket, opening a new one where the key has not been seen. Only "
                   + $"{(aggregates.Length > 0 ? aggregates : "the totals")} are held per group, never the rows, so the input batches "
                   + "are released as they are consumed."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Emit,
            Title = "Emit",
            Lead = "Once the input is exhausted the table is walked bucket by bucket and the groups are packed into a batch of this "
                   + "operator's own rather than the one they were read from, which is why the groups come out in hash order rather "
                   + "than the order they arrived."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends once every bucket has been walked."
        });

        return new OperatorDescription
        {
            Summary = $"Batch mode operator that groups on {groupBy}, reading batches into an in-memory hash table with an entry per "
                      + "group key and emitting the groups as batches of its own.",
            IsBlocking = true,
            Phases = phases.ToImmutable()
        };
    }
}
