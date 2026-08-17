using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Windowing;

public static class SegmentDescriber
{
    public static OperatorDescription Describe(SegmentDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        var columns = string.Join(", ", definition.GroupBy);

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Segment,
            Title = "Segment",
            Lead = columns.Length > 0
                ? $"Each row arriving from the input is compared with the one before it on {columns}, and passed on with "
                  + $"{definition.SegmentColumn} set when the values differ. Only the previous row's values are held, so the input has to "
                  + "already be ordered on those columns for a group to arrive in one piece."
                : $"There are no grouping columns, so the whole input is one segment and {definition.SegmentColumn} is set on the first "
                  + "row only. That is what an OVER clause with no PARTITION BY produces."
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The operator ends when its input runs out. Every row was passed on as it arrived, so there is nothing left to return."
        });

        return new OperatorDescription
        {
            Summary = "Operator that marks where one group of rows ends and the next begins, adding a flag column the operator above "
                      + "reads rather than dividing the rows up itself.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}
