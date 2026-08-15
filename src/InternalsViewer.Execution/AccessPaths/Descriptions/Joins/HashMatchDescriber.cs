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
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Size,
            Title = "Size",
            Lead = $"The bucket count is chosen from the {definition.Build.RowEstimate:N0} rows the build side is estimated to return, " +
                   "before a row is read. An estimate that is too low leaves long chains to walk and one that is too high leaves buckets " +
                   "empty"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Build,
            Title = "Build",
            Lead = "Read the build input to its end, hashing each row's key and adding the row to that bucket's chain. Nothing can leave " +
                   "the join while this runs, which is the blocking half of the operator"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Probe,
            Title = "Probe",
            Lead = "Read the probe input a row at a time, hash its key and walk only the bucket that hash selects. Rows start flowing " +
                   "here, which is the streaming half"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Compare,
            Title = "Compare",
            Lead = "An entry in the chain is still compared on the key itself, because equal hashes do not mean equal keys",
            Middle = PhaseCondition.Exists(definition.Residual) ? ". Once the keys match the residual " : string.Empty,
            Condition = PhaseCondition.Of(definition.Residual),
            Trail = PhaseCondition.Exists(definition.Residual) ? " decides the pair" : string.Empty
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = definition.JoinType is JoinType.Inner
                ? "The join ends when the probe input runs out"
                : "The join ends when the probe input runs out, after walking the hash table for the build rows nothing probed"
        });

        return new OperatorDescription
        {
            Summary = "Join that reads the build input in full into an in-memory hash table, then hashes each probe row to one bucket and " +
                      "compares it against only the rows in that bucket",
            IsStreaming = true,
            IsBlocking = true,
            Phases = phases.ToImmutable()
        };
    }
}
