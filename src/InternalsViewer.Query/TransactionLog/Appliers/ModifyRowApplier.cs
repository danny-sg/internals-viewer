using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_MODIFY_ROW log records
/// </summary>
/// <remarks>
/// - A same-size modification is a straight splice
///
/// - A size-changing modification rebuilds the row with the splice applied. A shrinking row is rewritten in place leaving its old tail as
///   a hole
///
/// - A growing row is extended in
///   place when it is the last row before the free data offset, otherwise it is relocated to the free data offset (rows always land at
///   free data - the same placement behaviour observed for inserts) leaving the old copy as a hole.
///
///   The physical layout can therefore differ from the engine's capture-time layout while staying logically identical - later records
/// address rows via the slot's offset table entry, which is kept up to date.
/// </remarks>
public sealed class ModifyRowApplier : PageLogRecordApplier<ModifyRowLogRecord>
{
    protected override ApplyResult ApplyRecord(PageData page, ModifyRowLogRecord record)
    {
        if (!TryGetSlotOffset(page, record.SlotId, out var rowOffset))
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Slot {record.SlotId} is not in the offset table");
        }

        if (record.AfterData.Length == record.ModifySize)
        {
            return ApplySplice(page,
                               rowOffset + record.OffsetInRow,
                               record.ModifySize,
                               record.BeforeData,
                               record.AfterData,
                               $"Slot {record.SlotId} row modified at row offset {record.OffsetInRow}");
        }

        return ApplyResize(page, record, rowOffset);
    }

    private static ApplyResult ApplyResize(PageData page, ModifyRowLogRecord record, int rowOffset)
    {
        if (!TryGetRowLength(page.Data, rowOffset, out var oldLength))
        {
            return new ApplyResult(ApplyStatus.NotSupported,
                                   $"Row at slot {record.SlotId} is not a supported record type");
        }

        if (record.OffsetInRow + record.ModifySize > oldLength)
        {
            return new ApplyResult(ApplyStatus.NotSupported,
                                   $"Modify range at row offset {record.OffsetInRow} is beyond the row's " +
                                   $"{oldLength} bytes");
        }

        var target = page.Data.AsSpan(rowOffset + record.OffsetInRow, record.ModifySize);

        if (record.BeforeData.Length > 0 && !target.SequenceEqual(record.BeforeData))
        {
            return new ApplyResult(ApplyStatus.BeforeImageMismatch,
                                   $"Row at slot {record.SlotId} does not match the record's before image");
        }

        var delta = record.AfterData.Length - record.ModifySize;

        var newLength = oldLength + delta;

        var newRow = new byte[newLength];

        page.Data.AsSpan(rowOffset, record.OffsetInRow).CopyTo(newRow);

        record.AfterData.CopyTo(newRow.AsSpan(record.OffsetInRow));

        page.Data.AsSpan(rowOffset + record.OffsetInRow + record.ModifySize,
                         oldLength - record.OffsetInRow - record.ModifySize)
            .CopyTo(newRow.AsSpan(record.OffsetInRow + record.AfterData.Length));

        var sizeDescription = delta < 0 ? $"shrunk by {-delta}" : $"grew by {delta}";

        return PlaceRebuiltRow(page,
                               record.SlotId,
                               rowOffset,
                               oldLength,
                               newRow,
                               record.OffsetInRow,
                               $"Slot {record.SlotId} row modified at row offset {record.OffsetInRow} " +
                               $"({sizeDescription} bytes)",
                               []);
    }
}
