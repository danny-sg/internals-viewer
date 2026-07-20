using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_DELETE_ROWS log records
/// </summary>
/// <remarks>
/// The row's bytes are left in place as a hole - only the offset table and header accounting change. A heap delete zeroes the slot's
/// offset entry, keeping the slot count and later slot ids (RIDs) stable. A b-tree delete shifts the offset table entries above SlotId
/// down by one.
/// </remarks>
public sealed class DeleteRowsApplier : PageLogRecordApplier<DeleteRowsLogRecord>
{
    protected override ApplyResult ApplyRecord(PageData page, DeleteRowsLogRecord record)
    {
        if (!TryGetSlotOffset(page, record.SlotId, out var rowOffset))
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Slot {record.SlotId} is not in the offset table");
        }

        if (rowOffset == 0)
        {
            return new ApplyResult(ApplyStatus.BeforeImageMismatch, $"Heap slot {record.SlotId} is already empty");
        }

        var rowLength = record.RowData.Length;

        if (rowLength > 0)
        {
            if (rowOffset + rowLength > page.Data.Length)
            {
                return new ApplyResult(ApplyStatus.NotSupported, $"Row at {rowOffset} overruns the page");
            }

            if (!page.Data.AsSpan(rowOffset, rowLength).SequenceEqual(record.RowData))
            {
                return new ApplyResult(ApplyStatus.BeforeImageMismatch,
                                       $"Row at slot {record.SlotId} does not match the record's row image");
            }
        }

        var header = page.PageHeader;

        var changes = new List<ChangeSpan>();

        if (record.Context == LogContext.HEAP)
        {
            WriteOffsetTableEntry(page, record.SlotId, 0);

            changes.Add(new ChangeSpan(GetOffsetTableEntryPosition(record.SlotId),
                                       sizeof(ushort),
                                       $"Slot offset table entry {record.SlotId} zeroed (row deleted)"));

            SetFreeCount(page, (ushort)(header.FreeCount + rowLength), changes);
        }
        else
        {
            for (var slotId = record.SlotId; slotId < header.SlotCount - 1; slotId++)
            {
                WriteOffsetTableEntry(page, slotId, page.OffsetTable[slotId + 1]);
            }

            changes.Add(new ChangeSpan(GetOffsetTableEntryPosition(header.SlotCount - 1),
                                       2 * (header.SlotCount - record.SlotId),
                                       $"Slot offset table entries {record.SlotId}-{header.SlotCount - 1} " +
                                       "shifted down (row deleted)"));

            SetSlotCount(page, (ushort)(header.SlotCount - 1), changes);
            SetFreeCount(page, (ushort)(header.FreeCount + rowLength + 2), changes);
        }

        RebuildOffsetTable(page);

        return ApplyResult.Applied(changes);
    }
}
