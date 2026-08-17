using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Aggregation;

public static class HashAggregateDescriber
{
    public static OperatorDescription Describe(HashAggregateDefinition definition)
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
            Lead = $"Each row is hashed on {groupBy} and folded into the group sitting in that bucket, opening a new one when the key "
                   + $"has not been seen. Only {(aggregates.Length > 0 ? aggregates : "the totals")} are held per group, never the rows."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Emit,
            Title = "Emit",
            Lead = "Once the input is exhausted the table is walked bucket by bucket and each group is returned as one row, which is "
                   + "why the rows come out in hash order rather than in the order they arrived."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends once every bucket has been walked."
        });

        return new OperatorDescription
        {
            Summary = $"Groups on {groupBy} by taking unordered input and accumulating aggregations. Reads the build input in full into an " +
                      $"in-memory hash table with an entry per group key. Groups are progressively added or updated with a calculated " +
                      $"hash bucket and hash value, and key value.",
            IsBlocking = true,
            Phases = phases.ToImmutable()
        };
    }
}
