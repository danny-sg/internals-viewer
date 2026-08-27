using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.BatchMode;

public static class ColumnstoreScanDescriber
{
    public static OperatorDescription Describe(ColumnstoreScanDefinition definition)
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.SegmentElimination,
            Title = "Segment Elimination",
            Lead = "The minimum and maximum value metadata for each projected segment is checked against the predicate. If one or more "
                   + "segments cannot hold a matching value the row group is skipped without reading anything. A dictionary encoded "
                   + "segment holds ids rather than values, so its range says nothing about the values it stores and it cannot be "
                   + "eliminated here.",
            Condition = PhaseCondition.Of(definition.Residual)
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.RowGroup,
            Title = "Row Group",
            Lead = "The row group is opened. Dictionaries are loaded first, then a segment for each projected column. A global "
                   + "dictionary is shared by every row group, so it is read once for the scan.",
            Middle = "Reading a dictionary here is what lets a dictionary encoded column whose value is in no entry eliminate the row "
                     + "group at this point rather than during elimination. " + FilterDescription(definition)
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Walk,
            Title = "Get Batch",
            Lead = $"The row group is read a window of up to {definition.BatchRowCount} rows at a time into the same batch, which is "
                   + "reset rather than allocated again. Every row starts selected, then the delete bitmap read at open clears the rows "
                   + "deleted in this window.",
            Middle = definition.IsFilterOnCompressedDataUsed
                     ? "Filters that can be answered from the compressed data run next, so a run of repeated values is tested once "
                       + "rather than once per row. Only the rows still selected are then decoded into the vectors, and a window where "
                       + "nothing survives is read again from the next window rather than passed on."
                     : "Only the rows still selected are decoded into the vectors, and a window where nothing survives is read again "
                       + "from the next window rather than passed on."
        });

        if (HasPredicate(definition))
        {
            phases.Add(new AccessStrategyPhase
            {
                Phase = AccessPhase.Filter,
                Title = "Filter Vector",
                Lead = "A predicate that could not be answered from the compressed data is evaluated over the decoded values, so it "
                       + "runs after the vectors are filled and only for the rows still selected. The selection is then narrowed again.",
                Condition = PhaseCondition.Of(definition.Residual)
            });
        }

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The scan ends when every qualifying row group has been read. An operator above that has all the rows it needs stops "
                   + "asking for batches, which ends the scan early without it having to know why."
        });

        return new OperatorDescription
        {
            Summary = "Batch mode operator that reads from a columnstore index.",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }

    private static bool HasPredicate(ColumnstoreScanDefinition definition)
        => definition.IsGenericFilterUsed || definition.Residual is not null;

    private static string FilterDescription(ColumnstoreScanDefinition definition)
    {
        var hasPredicate = HasPredicate(definition);

        if (definition.IsFilterOnCompressedDataUsed && hasPredicate)
        {
            return "The predicate is filtered against the compressed data, and the part of it that cannot be answered that way is "
                   + "filtered against the decoded values.";
        }

        if (definition.IsFilterOnCompressedDataUsed)
        {
            return "The predicate is filtered against the compressed data.";
        }

        return hasPredicate
               ? "The predicate cannot be answered from the compressed data, so it is filtered against the decoded values."
               : string.Empty;
    }
}
