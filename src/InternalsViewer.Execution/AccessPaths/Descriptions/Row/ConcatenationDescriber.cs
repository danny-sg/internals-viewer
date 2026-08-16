using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Row;

public static class ConcatenationDescriber
{
    public static OperatorDescription Describe(ConcatenationDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Inputs,
            Title = "Inputs",
            Lead = $"The {definition.Inputs.Count} inputs are read in the order the plan lists them, one opened only once the input " +
                   "before it is exhausted."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Pass,
            Title = "Pass",
            Lead = "Every row of the current input is passed up as it arrives, mapped onto the common output columns."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends when the last input is exhausted."
        });

        return new OperatorDescription
        {
            Summary = "Operator that reads its inputs one after another and passes every row through unchanged, with no ordering or " +
                      "duplicate removal of its own.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
