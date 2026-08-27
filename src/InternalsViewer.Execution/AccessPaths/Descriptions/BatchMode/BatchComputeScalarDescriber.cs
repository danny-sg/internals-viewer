using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.BatchMode;

public static class BatchComputeScalarDescriber
{
    public static OperatorDescription Describe(BatchComputeScalarDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        var columns = string.Join(", ", definition.Columns.Select(c => c.Name));

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Compute,
            Title = "Compute",
            Lead = columns.Length > 0
                ? $"The batch is widened by a vector for {columns}, worked out from the vectors the batch already carries. Only rows "
                  + "still in the selection vector are computed, so rows an earlier filter dropped cost nothing."
                : "The batch is widened by a vector for each expression this operator defines, computed only for the rows still selected."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends when its input runs out. Nothing is held, so there is nothing left to return."
        });

        return new OperatorDescription
        {
            Summary = "Batch mode operator that adds a vector to the batch for each expression it defines, passing the batch on rather "
                      + "than building a row.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
