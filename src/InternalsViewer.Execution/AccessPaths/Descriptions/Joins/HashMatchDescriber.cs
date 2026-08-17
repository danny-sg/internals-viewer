using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Joins;

public static class HashMatchDescriber
{
    public static OperatorDescription Describe(HashMatchDefinition definition)
    {
        return new OperatorDescription
        {
            Summary = "Join that reads the build input in full into an in-memory hash table, then hashes each probe row to find a match " +
                      "based on a lookup using the built hash table, progressively checking hash bucket, hash value, and key value.",
            IsStreaming = true,
            IsBlocking = true,
            Phases =
            [
                new AccessStrategyPhase
                {
                    Phase = AccessPhase.Buckets,
                    Title = "Buckets",
                    Lead = $"Bucket count is chosen from the {definition.Build.RowEstimate:N0} rows the build side is estimated to return, " +
                           "unless the plan gave one outright, and the hash table pane can resize it while the trace runs." +
                           "\nInternals Viewer approximates the sizing SQL Server uses, so the count can differ from the real one, and " +
                           "approximates its hash function, so a row can sit in a different bucket than SQL Server would put it in"
                },
                new AccessStrategyPhase
                {
                    Phase = AccessPhase.Build,
                    Title = "Build",
                    Lead = "Read the build input to its end. Each row key is hashed and added to the hash table with bucket, hash, and key value." +
                           "\nEntries are chained if a bucket holds more than one." +
                           "\nThe Build phase is blocking because all input must be read to build the hash table before the probe phase can begin."
                },
                new AccessStrategyPhase
                {
                    Phase = AccessPhase.Probe,
                    Title = "Probe",
                    Lead = "Read the probe input a row at a time, hashing the key to derive a hash value and the bucket it selects. The bucket " +
                           "is found in the hash table, then the hash value is compared against each entry in the bucket's chain."
                },
                new AccessStrategyPhase
                {
                    Phase = AccessPhase.Compare,
                    Title = "Compare",
                    Lead = "Compares the key values for a matching hash",
                    Middle = PhaseCondition.Exists(definition.Residual) ? ". Once the keys match the residual " : string.Empty,
                    Condition = PhaseCondition.Of(definition.Residual),
                    Trail = PhaseCondition.Exists(definition.Residual) ? " decides the pair." : "."
                },
                new AccessStrategyPhase
                {
                    Phase = AccessPhase.Complete,
                    Title = "Complete",
                    Lead = definition.JoinType is JoinType.Inner
                        ? "The join ends when the probe input runs out."
                        : "The join ends when the probe input runs out, after walking the hash table for the build rows nothing probed."
                },
            ]
        };
    }
}
