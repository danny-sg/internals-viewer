using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Row;

public static class ComputeScalarDescriber
{
    public static OperatorDescription Describe(ComputeScalarDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        var columns = string.Join(", ", definition.Columns.Select(c => c.Name));

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Compute,
            Title = "Compute",
            Lead = columns.Length > 0
                ? $"Each row arriving from the input is given {columns}, worked out from the columns the row already carries."
                : "Each row arriving from the input is passed on with the expressions this operator defines added to it."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends when its input runs out. Nothing is held, so there is nothing left to return."
        });

        return new OperatorDescription
        {
            Summary = "Operator that evaluates expressions over each row and adds them as columns, passing the row straight through.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
