using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_MODIFY_COLUMNS log records
/// </summary>
/// <remarks>
/// When every region keeps its size, each region is applied as a splice against the row in place. When any region
/// changes size, the row is rebuilt by walking the regions in row order - unchanged stretches copied from the old
/// row, each region's after image substituted for its before length - and placed via the shared shrink in place /
/// grow at free data behaviour.
/// </remarks>
public sealed class ModifyColumnsApplier : PageLogRecordApplier<ModifyColumnsLogRecord>
{
    protected override ApplyResult ApplyRecord(PageData page, ModifyColumnsLogRecord record)
    {
        if (!TryGetSlotOffset(page, record.SlotId, out var rowOffset))
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Slot {record.SlotId} is not in the offset table");
        }

        if (record.Modifications.All(m => m.BeforeLength == m.AfterData.Length))
        {
            return ApplySameSize(page, record, rowOffset);
        }

        return ApplyResize(page, record, rowOffset);
    }

    private static ApplyResult ApplySameSize(PageData page, ModifyColumnsLogRecord record, int rowOffset)
    {
        var changes = new List<ChangeSpan>();

        foreach (var modification in record.Modifications)
        {
            var result = ApplySplice(page,
                                     rowOffset + modification.AfterOffset,
                                     modification.BeforeLength,
                                     modification.BeforeData,
                                     modification.AfterData,
                                     $"Slot {record.SlotId} row modified at row offset {modification.AfterOffset}");

            if (!result.IsApplied)
            {
                return result;
            }

            changes.AddRange(result.Changes);
        }

        return ApplyResult.Applied(changes);
    }

    private static ApplyResult ApplyResize(PageData page, ModifyColumnsLogRecord record, int rowOffset)
    {
        if (!TryGetRowLength(page.Data, rowOffset, out var oldLength))
        {
            return new ApplyResult(ApplyStatus.NotSupported,
                                   $"Row at slot {record.SlotId} is not a supported record type");
        }

        var regions = record.Modifications.OrderBy(m => m.BeforeOffset).ToList();

        var newLength = oldLength + regions.Sum(m => m.AfterData.Length - m.BeforeLength);

        if (newLength <= 0)
        {
            return new ApplyResult(ApplyStatus.NotSupported, "Modifications remove the entire row");
        }

        var newRow = new byte[newLength];

        var oldPosition = 0;

        var newPosition = 0;

        foreach (var region in regions)
        {
            var gap = region.BeforeOffset - oldPosition;

            if (gap < 0 || region.BeforeOffset + region.BeforeLength > oldLength)
            {
                return new ApplyResult(ApplyStatus.NotSupported,
                                       $"Region at row offset {region.BeforeOffset} overlaps another region or " +
                                       "is beyond the row");
            }

            page.Data.AsSpan(rowOffset + oldPosition, gap).CopyTo(newRow.AsSpan(newPosition));

            oldPosition += gap;
            newPosition += gap;

            if (region.AfterOffset != newPosition)
            {
                return new ApplyResult(ApplyStatus.NotSupported,
                                       $"Region after offset {region.AfterOffset} does not match the rebuilt " +
                                       $"row position {newPosition}");
            }

            var target = page.Data.AsSpan(rowOffset + oldPosition, region.BeforeLength);

            if (region.BeforeData.Length == region.BeforeLength && !target.SequenceEqual(region.BeforeData))
            {
                return new ApplyResult(ApplyStatus.BeforeImageMismatch,
                                       $"Row at slot {record.SlotId} does not match the record's before image " +
                                       $"at row offset {region.BeforeOffset}");
            }

            region.AfterData.CopyTo(newRow.AsSpan(newPosition));

            oldPosition += region.BeforeLength;
            newPosition += region.AfterData.Length;
        }

        page.Data.AsSpan(rowOffset + oldPosition, oldLength - oldPosition).CopyTo(newRow.AsSpan(newPosition));

        var delta = newLength - oldLength;

        var sizeDescription = delta < 0 ? $"shrunk by {-delta}" : $"grew by {delta}";

        return PlaceRebuiltRow(page,
                               record.SlotId,
                               rowOffset,
                               oldLength,
                               newRow,
                               regions[0].AfterOffset,
                               $"Slot {record.SlotId} row modified in {regions.Count} region(s) " +
                               $"({sizeDescription} bytes)",
                               []);
    }
}
