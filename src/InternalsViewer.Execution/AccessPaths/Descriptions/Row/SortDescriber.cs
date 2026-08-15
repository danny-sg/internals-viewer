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
                ? $"Read every row of the input, retaining only the {topCount:N0} that sort highest. A row that sorts below the ones " +
                  "already held is dropped as it arrives, so the memory the sort needs is bounded by the count rather than by the input"
                : "Read every row of the input into the sort. Nothing can be emitted yet, which is what makes the operator blocking and " +
                  "why the whole set has to fit in the memory granted to it"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Sort,
            Title = "Sort",
            Lead = "Order the collected rows on the sort keys"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Emit,
            Title = "Emit",
            Lead = "Hand the ordered rows up one at a time. Everything above this operator sees rows only from here on"
        });

        if (definition.IsDistinct)
        {
            phases.Add(new AccessStrategyPhase
            {
                Phase = AccessPhase.Duplicate,
                Title = "Duplicate",
                Lead = "A row equal on every key to the one emitted before it is dropped, which is how a distinct sort removes " +
                       "duplicates once the ordering has brought them together"
            });
        }

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The sort ends when the ordered rows have all been emitted"
        });

        return new OperatorDescription
        {
            Summary = "Blocking operator that reads its whole input before returning a single row, because the row that sorts first can " +
                      "be the last one read",
            IsBlocking = true,
            Phases = phases.ToImmutable()
        };
    }
}
