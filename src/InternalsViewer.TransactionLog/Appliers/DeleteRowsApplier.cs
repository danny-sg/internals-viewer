using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_DELETE_ROWS log records
/// </summary>
/// <remarks>
/// A physical delete leaves the row's bytes in place as a hole - only the offset table and header accounting change. A heap delete zeroes
/// the slot's offset entry, keeping the slot count and later slot ids (RIDs) stable. A b-tree delete shifts the offset table entries above
/// SlotId down by one.
///
/// A ghost delete (LCX_MARK_AS_GHOST) is materially different - the row is NOT removed. It stays in its slot with its record type changed
/// to a ghost type, and the page header's ghost record count (which is not separately logged) is incremented.
/// </remarks>
public sealed class DeleteRowsApplier : PageLogRecordApplier<DeleteRowsLogRecord>
{
    private const int RecordTypeMask = 0x0E;

    private const int IndexRecordType = 3;

    private const int GhostIndexRecordType = 5;

    private const int GhostDataRecordType = 6;

    protected override ApplyResult ApplyRecord(PageData page, DeleteRowsLogRecord record)
    {
        if (!TryGetSlotOffset(page, record.SlotId, out var rowOffset))
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Slot {record.SlotId} is not in the offset table");
        }

        if (record.Context == LogContext.MARK_AS_GHOST)
        {
            return ApplyGhostMark(page, record, rowOffset);
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

    /// <summary>
    /// Marks the row at the slot as a ghost - the row stays in place, its record type changes to the ghost type and
    /// the header ghost record count is incremented
    /// </summary>
    /// <remarks>
    /// The record type lives in bits 1-3 of the row's first status byte. A live data record (primary) ghosts to
    /// GHOST_DATA_RECORD and an index record ghosts to GHOST_INDEX_RECORD. The ghost record count is not carried by
    /// any log record, so it is maintained here.
    /// </remarks>
    private static ApplyResult ApplyGhostMark(PageData page, DeleteRowsLogRecord record, int rowOffset)
    {
        var statusA = page.Data[rowOffset];

        var recordType = (statusA >> 1) & 0x7;

        if (recordType is GhostIndexRecordType or GhostDataRecordType)
        {
            return ApplyResult.Success;
        }

        var ghostType = recordType == IndexRecordType ? GhostIndexRecordType : GhostDataRecordType;

        var newStatusA = (byte)((statusA & ~RecordTypeMask) | (ghostType << 1));

        page.Data[rowOffset] = newStatusA;

        var changes = new List<ChangeSpan>
        {
            new(rowOffset, sizeof(byte), $"Slot {record.SlotId} record marked as ghost in Status Bits A")
        };

        SetGhostCount(page, (short)(page.PageHeader.GhostRecordCount + 1), changes);

        return ApplyResult.Applied(changes);
    }
}
