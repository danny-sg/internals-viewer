using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Interfaces.Pages;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Execution.Executors;

/// <summary>
/// Executes an allocation scan record read on a page
/// </summary>
internal static class AllocationScanExecutor
{
    public static IEnumerable<AccessStep> Execute(IRowPageAccessor page, PageWalk walk)
    {
        return Walk(page, walk);
    }

    /// <summary>
    /// Reads a page, iterating through the slots
    /// </summary>
    /// <remarks>
    /// Checks if a record is a ghost record (marked as deleted)
    ///
    /// Outputs the row Access Steps using RowStepBuilder.
    /// </remarks>
    private static IEnumerable<AccessStep> Walk(IRowPageAccessor page, PageWalk walk)
    {
        var totals = walk.Counters.AddPageRead();

        yield return new AccessStep.ReadPage(page.PageAddress, page.Level, false, page.IsLeaf, page.SlotCount)
        {
            IsHeap = page.Structure == StructureType.Heap,
            Counters = totals
        };

        for (var slot = 0; slot < page.SlotCount; slot++)
        {
            if (page.GetRecord(slot).IsGhost)
            {
                var ghost = RowStepBuilder.Ghost(walk, slot, totals, hasRange: false);

                totals = ghost.Counters;

                yield return ghost;

                continue;
            }

            foreach (var step in RowStepBuilder.Examine(page, walk, slot, totals, hasRange: false))
            {
                totals = step.Counters;

                yield return step;

                if (step is AccessStep.Stopped)
                {
                    yield break;
                }
            }
        }
    }
}
