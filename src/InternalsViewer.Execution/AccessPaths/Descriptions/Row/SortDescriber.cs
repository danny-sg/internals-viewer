using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Row;

public static class SortDescriber
{
    public static OperatorDescription Describe(SortDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Collect,
            Title = "Collect",
            Lead = definition.TopCount is { } topCount
                ? $"Read every row of the input, retaining only the {topCount:N0} in the sort buffer based on the sort keys."
                : "Read every row of the input into the sort buffer."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Sort,
            Title = "Sort",
            Lead = "Order the collected rows on the sort keys in the sort buffer." 
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Emit,
            Title = "Emit",
            Lead = "Emit each row from the sorted sort buffer."
        });

        if (definition.IsDistinct)
        {
            phases.Add(new AccessStrategyPhase
            {
                Phase = AccessPhase.Duplicate,
                Title = "Duplicate",
                Lead = "Check against the previous row emitted to remove duplicates."
            });
        }

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "Sort ends when the ordered rows have all been emitted."
        });

        return new OperatorDescription
        {
            Summary = $"Operator that fully reads input rows, sorts them based on the sort keys, then emits rows in sorted order" 
                      + (definition.IsDistinct ? ", eliminating duplicates." : "."),
            IsBlocking = true,
            Phases = phases.ToImmutable()
        };
    }
}
