using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_INSERT_ROWS log records
/// </summary>
/// <remarks>
/// The row image is always placed at the free data offset - existing row data never moves. A heap insert either appends a new slot
/// (SlotId equal to the slot count) or reuses a zeroed entry left by an earlier delete. A b-tree insert at an interior position shifts
/// the offset table entries above SlotId up by one.
/// </remarks>
public sealed class InsertRowsApplier : PageLogRecordApplier<InsertRowsLogRecord>
{
    protected override ApplyResult ApplyRecord(PageData page, InsertRowsLogRecord record)
    {
        var rowLength = record.RowData.Length;

        if (rowLength == 0)
        {
            return new ApplyResult(ApplyStatus.NotSupported, "Insert has no row data");
        }

        var header = page.PageHeader;

        if (record.SlotId > header.SlotCount)
        {
            return new ApplyResult(ApplyStatus.NotSupported,
                                   $"Slot {record.SlotId} is beyond the slot count {header.SlotCount}");
        }

        var addsEntry = record.SlotId == header.SlotCount || record.Context != LogContext.HEAP;

        var offsetTableStart = PageData.Size - 2 * (header.SlotCount + (addsEntry ? 1 : 0));

        var rowOffset = header.FreeData;

        if (rowOffset + rowLength > offsetTableStart)
        {
            return new ApplyResult(ApplyStatus.NotSupported,
                                   "Not enough contiguous free space (page needs compaction)");
        }

        if (!addsEntry && page.OffsetTable[record.SlotId] != 0)
        {
            return new ApplyResult(ApplyStatus.BeforeImageMismatch,
                                   $"Heap slot {record.SlotId} is in use at offset {page.OffsetTable[record.SlotId]}");
        }

        var changes = new List<ChangeSpan>();

        if (addsEntry && record.SlotId < header.SlotCount)
        {
            for (var slotId = header.SlotCount; slotId > record.SlotId; slotId--)
            {
                WriteOffsetTableEntry(page, slotId, page.OffsetTable[slotId - 1]);
            }

            changes.Add(new ChangeSpan(GetOffsetTableEntryPosition(header.SlotCount),
                                       2 * (header.SlotCount - record.SlotId),
                                       $"Slot offset table entries {record.SlotId + 1}-{header.SlotCount} " +
                                       "shifted up"));
        }

        record.RowData.CopyTo(page.Data.AsSpan(rowOffset));

        changes.Add(new ChangeSpan(rowOffset, rowLength, $"Row data inserted at slot {record.SlotId}"));

        WriteOffsetTableEntry(page, record.SlotId, rowOffset);

        changes.Add(new ChangeSpan(GetOffsetTableEntryPosition(record.SlotId),
                                   sizeof(ushort),
                                   addsEntry
                                       ? $"Record added to slot offset table (slot {record.SlotId})"
                                       : $"Slot offset table entry {record.SlotId} reused"));

        if (addsEntry)
        {
            SetSlotCount(page, (ushort)(header.SlotCount + 1), changes);
        }

        SetFreeData(page, (ushort)(rowOffset + rowLength), changes);
        SetFreeCount(page, (ushort)(header.FreeCount - rowLength - (addsEntry ? 2 : 0)), changes);

        RebuildOffsetTable(page);

        return ApplyResult.Applied(changes);
    }
}
