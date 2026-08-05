using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Trace;

public static class TraceStepDescriber
{
    public static string Describe(AccessStep step, IReadOnlyDictionary<int, TraceStepNode>? nodes)
    {
        var node = nodes?.GetValueOrDefault(step.NodeId);

        var body = step switch
        {
            AccessStep.HashBuild build =>
                $"Build - Adds the row to the hash table for key {build.Key}.\n\n"
                + $"The key is hashed to 0x{build.Hash:X8}, which selects bucket {build.Bucket}. The row is appended to that bucket's "
                + $"chain as entry {build.ChainLength}. Rows sharing a bucket are walked in order when a probe compares against it."
                + (build.IsNullKey ? "\n\nThe key holds a NULL, so this row occupies a bucket but can never match a probe." : string.Empty),

            AccessStep.HashProbe { IsNullKey: true } probe =>
                $"Probe - Probes the hash table for {probe.Key}.\n\n"
                + "The key holds a NULL, which never equals anything, so no bucket is walked and the row cannot match.",

            AccessStep.HashProbe probe =>
                $"Probe - Probes the hash table for {probe.Key}.\n\n"
                + $"The hash of the key is 0x{probe.Hash:X8}, which selects bucket {probe.Bucket} holding {probe.ChainLength} "
                + "entries. Each entry on the chain is compared: first the stored hash, then the key itself, and where the keys match "
                + "any residual predicate decides the outcome.",

            AccessStep.HashCompare compare =>
                $"Compare - Tests the probe row against entry {compare.Entry} of bucket {compare.Bucket}.\n\n"
                + (compare.IsMatch
                    ? "The hashes matched, the keys matched and the pair passed the join's conditions, so this pair is a match."
                    : compare.IsFalsePositive
                        ? "The hashes collided but the keys differ. This is the comparison a wider hash would have avoided."
                        : compare.IsResidualFail
                            ? "The keys matched but the residual predicate rejected the pair."
                            : "The stored hash differs, so the pair is rejected without a key comparison."),

            AccessStep.MergeCompare compare =>
                $"Compare - Compares the current outer and inner keys.\n\n"
                + $"{compare.OuterKey} versus {compare.InnerKey}: {compare.Action}. Both inputs arrive in key order, so the side "
                + "that is behind advances until the keys meet.",

            AccessStep.MergeCompareRun run =>
                $"Compare - {run.Count} comparisons advanced the same side.\n\n"
                + $"The outer moved from {run.OuterFrom} to {run.OuterTo} and the inner from {run.InnerFrom} to {run.InnerTo}. "
                + "One side held a key ahead of the other, so the walk caught up without a match.",

            AccessStep.JoinEmit emit =>
                emit.IsUnmatched
                    ? "Emit - Outputs a preserved row with no partner.\n\n"
                      + "No row on the other side matched, and the join type preserves this side, so the row is output with NULLs "
                      + "in place of the missing columns."
                    : $"Emit - Outputs joined row {emit.PairNumber}.\n\n"
                      + "The pair matched on the join keys and passed the join's conditions, so the combined row is handed to the "
                      + "operator above.",

            AccessStep.JoinVerdict verdict =>
                $"Verdict - The {verdict.Decision.JoinName} join weighed this pairing and "
                + (verdict.Decision.IsEmitted ? "a row is emitted." : "nothing is emitted."),

            AccessStep.Row row =>
                $"Get Row - Returns the row in slot {row.Slot}.\n\n"
                + row.Outcome switch
                {
                    RowOutcome.Match => row.HasResidual
                        ? "The row is inside the range and passed the residual predicate, so it is output."
                        : "The row is inside the range, so it is output.",
                    RowOutcome.NoMatch => "The row was read but failed the residual predicate, so it is skipped.",
                    RowOutcome.Ghost => "The slot holds a ghost record, deleted but not yet cleaned up, so it is skipped without "
                                        + "a comparison.",
                    _ => "The residual predicate could not be decided for this row, so it is treated as not matching."
                },

            AccessStep.RowRun run =>
                $"Get Row - Walked slots {run.FromSlot} to {run.ToSlot}, outputting {run.EmitCount} of {run.Count} rows.",

            AccessStep.ReadPage read =>
                $"Read Page - Reads page {read.PageAddress}.\n\n"
                + (read.IsHeap
                    ? "The page is a heap data page, named directly by a row identifier rather than reached by a descent."
                    : read.IsRoot && read.IsLeaf
                        ? $"The index is a single page deep, so the root is also the leaf, holding {read.SlotCount} rows."
                        : read.IsRoot
                            ? $"This is the root page of the index, where every descent starts. Its {read.SlotCount} slots each hold "
                              + "the lowest key of the page below, so comparing the seek key against them chooses which branch of the "
                              + "tree to follow."
                            : read.IsLeaf
                                ? $"The page is a leaf holding {read.SlotCount} rows. The descent has reached the level where the rows "
                                  + "themselves live, and the walk along the range starts here."
                                : $"The page is an index page at level {read.Level} holding {read.SlotCount} downlinks, one level "
                                  + "closer to the leaf."),

            AccessStep.Descend descend =>
                $"Descend - Follows the downlink in slot {descend.Slot} to page {descend.ChildPage}.\n\n"
                + "The slot holds the lowest key of the child page below it, so the search continues one level closer to the leaf.",

            AccessStep.ProbeStart start =>
                $"Search - Starts a binary search of {start.SlotCount} slots.\n\n"
                + "The page's slot array is kept in key order, so instead of comparing every slot the search halves the window on each "
                + "probe. It is looking for the position where the seek predicate first holds - the slot the descent follows down, or "
                + "on a leaf the slot where the walk begins.",

            AccessStep.Probe probe =>
                $"Search - Probes slot {probe.Middle} in the window {probe.Low} to {probe.High}.\n\n"
                + $"The key {probe.Key} is compared against the target {probe.Target}, halving the window "
                + (probe.SearchRight ? "to the right." : "to the left."),

            AccessStep.ProbeResult result =>
                $"Position - The search settled on slot {result.Slot}."
                + (result.PastEnd ? "\n\nThe target is past the last slot of the page." : string.Empty),

            AccessStep.RangeEnd rangeEnd =>
                $"Range End - The key {rangeEnd.Key} is past the end of the range {rangeEnd.Boundary}, so the walk stops.",

            AccessStep.LeafLink link =>
                $"Next Page - Follows the leaf level link from {link.FromPage} to {link.ToPage}.\n\n"
                + "Leaf pages are chained in key order, so the walk continues without another descent.",

            AccessStep.Reseek reseek =>
                $"Reseek - Starts range {reseek.RangeNumber} of {reseek.RangeCount} with a fresh descent from the root.",

            AccessStep.Rebind rebind =>
                rebind.RowIdentifier is { } rid
                    ? $"Rebind - Re-opens the inner side to fetch the heap row at {rid}.\n\n"
                      + "The outer row carries the row identifier, so the page and slot are addressed directly with nothing to search."
                    : $"Rebind - Re-opens the inner side seeking {rebind.Key}.\n\n"
                      + "The seek key is bound from the current outer row, so the inner side descends its index again for this "
                      + "one lookup.",

            AccessStep.JoinStart start =>
                $"Phase - {start.Description}.",

            AccessStep.TopStart topStart =>
                $"Open - The TOP will stop its input after {topStart.RowCount:N0} rows.",

            AccessStep.InputStart inputStart =>
                $"Input - Starts reading input {inputStart.Number} of {inputStart.Count}.\n\n"
                + "A concatenation reads its inputs one after another, so each input is opened only once the one before it has run "
                + "out of rows.",

            AccessStep.ConcatRow concatRow =>
                $"Get Row - Passes through row {concatRow.Number:N0} from input {concatRow.InputNumber}.",

            AccessStep.SortCollect collect =>
                $"Collect - Reads row {collect.Number:N0} from the input into the sort table.\n\n"
                + (collect.IsRetained
                    ? "A sort is blocking: every input row has to be collected before the first row can be output, because the "
                      + "smallest row might be the last one read."
                    : "The sort keeps only the top rows, and this row sorts after all of them, so it is dropped rather than held."),

            AccessStep.Sorted sortedStep =>
                $"Sort - Sorted {sortedStep.Rows:N0} rows.\n\n"
                + "The input is exhausted, so the collected rows are ordered by the sort keys in one pass. From here rows are "
                + "output from the sort table in order.",

            AccessStep.SortRow sortRow =>
                $"Get Row - Outputs row {sortRow.Number:N0} from the sort table.",

            AccessStep.SortDuplicate sortDuplicate =>
                $"Duplicate - Skips a row whose sort key equals the one just output.\n\n"
                + "A distinct sort removes duplicates as it outputs: the rows are already in key order, so equal rows sit next to "
                + "each other and only the first of each group is returned.",

            SortCollectSpan span =>
                $"Collect - {span.Progress.Rows:N0} rows read into the sort table so far.",

            AccessStep.TopRow topRow =>
                $"Get Row - Passes through row {topRow.Number:N0} of {topRow.RowCount:N0}."
                + (topRow.IsLast ? "\n\nThe limit is reached, so the input is closed rather than read any further." : string.Empty),

            AccessStep.Open =>
                "Open - Prepares the operator to produce rows.\n\n"
                + "Open is called once by the operator above before the first row is requested, and cascades down as each operator "
                + "opens its own inputs in turn.",

            AccessStep.Close =>
                "Close - Shuts the operator down.\n\n"
                + "Close is called once no more rows will be requested, and cascades down so every input releases what it holds.",

            AccessStep.Output output =>
                $"Get Row - Returns row {output.Number:N0} to the client.\n\n"
                + "The SELECT sits above the plan's root operator and passes each requested row through unchanged.",

            AccessStep.Stopped stopped =>
                $"Stopped - No more rows: {stopped.Reason}.",

            AccessStep.ForwardedRecord forwarded =>
                $"Forward - The slot at {forwarded.From} holds a forwarding stub pointing to {forwarded.To}.\n\n"
                + "The row outgrew its page and moved, leaving the stub behind so the row identifier stays valid at the cost of "
                + "a second page read.",

            AccessStep.IamRead iam =>
                $"Read IAM - Reads the IAM page {iam.PageAddress}, mapping {iam.ExtentCount} extents and {iam.SinglePageCount} "
                + "single page slots.",

            AccessStep.PfsRead pfs =>
                $"Read PFS - Reads the PFS page {pfs.PageAddress} covering pages from {pfs.IntervalStartPage}.",

            AccessStep.PfsCheck check =>
                $"Check PFS - Page {check.PageAddress} is {(check.IsAllocated ? "allocated" : "not allocated")} ({check.Status}).",

            AccessStep.PageSkipped skipped =>
                $"Skip Page - Skips page {skipped.PageAddress}: {skipped.Reason}.",

            AccessStep.ExtentStart extent =>
                $"Next Extent - Starts extent {extent.ExtentIndex} at page {extent.FirstPage}.",

            AccessStep.Advance advance =>
                $"Advance - {advance.Description}.",

            HashBuildSpan span =>
                $"Build - {span.Progress.Count:N0} rows hashed into the table so far.",

            HashProbeSpan span =>
                $"Probe - {span.Progress.Rows:N0} rows probed, {span.Progress.Comparisons:N0} comparisons, "
                + $"{span.Progress.Matches:N0} matches, {span.Progress.Emits:N0} rows output.",

            HashMatchSpan span =>
                $"Match - {span.Progress.Matches:N0} matching pairs, {span.Progress.Emits:N0} rows output.\n\n"
                + $"The latest match compared entry {span.Progress.Entry} of bucket {span.Progress.Bucket} and output "
                + $"pair {span.Progress.PairNumber}.",

            RowCountSpan span =>
                span.Progress.Limit > 0
                    ? $"Get Row - Passed through row {span.Progress.Rows:N0} of {span.Progress.Limit:N0}."
                    : $"Get Row - Returned row {span.Progress.Rows:N0} to the client.",

            MergeCompareSpan span =>
                $"Compare - {span.Progress.Count:N0} comparisons advanced the same side.\n\n"
                + $"The {(span.Progress.Direction < 0 ? "outer" : "inner")} moved from {span.Progress.MovedFrom} to "
                + $"{span.Progress.MovedTo} against {span.Progress.StaticKey}. One side held a key ahead of the other, so the walk "
                + "caught up without a match.",

            MergeMatchSpan span =>
                $"Match - {span.Progress.Matches:N0} matching keys, {span.Progress.Emits:N0} rows output.\n\n"
                + $"The latest match paired the keys at {span.Progress.Key} and output pair {span.Progress.PairNumber}.",

            _ => step.GetType().Name
        };

        return node is { Summary.Length: > 0 } && IncludesSummary(step)
            ? $"{body}\n\n{node.Summary}"
            : body;
    }

    private static bool IncludesSummary(AccessStep step)
        => step is AccessStep.HashProbe
                or AccessStep.HashCompare
                or AccessStep.MergeCompare
                or AccessStep.MergeCompareRun
                or AccessStep.JoinEmit
                or AccessStep.JoinVerdict
                or AccessStep.TopRow
                or HashProbeSpan;
}
