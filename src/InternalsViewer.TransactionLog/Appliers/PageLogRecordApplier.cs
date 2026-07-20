using System.Buffers.Binary;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.TransactionLog.Appliers;

/// <summary>
/// Base class providing the page image operations shared by the log record appliers
/// </summary>
public abstract class PageLogRecordApplier
{
    private const int SlotCountOffset = 22;

    private const int FreeCountOffset = 28;

    private const int FreeDataOffset = 30;

    private const int GhostCountOffset = 58;

    private const int HeaderLsnOffset = 40;

    internal static ChangeSpan StampLsn(PageData page, LogSequenceNumber lsn)
    {
        var span = page.Data.AsSpan(HeaderLsnOffset);

        BinaryPrimitives.WriteInt32LittleEndian(span, lsn.VirtualLogFile);
        BinaryPrimitives.WriteInt32LittleEndian(span[sizeof(int)..], lsn.FileOffset);
        BinaryPrimitives.WriteInt16LittleEndian(span[(2 * sizeof(int))..], lsn.RecordSequence);

        page.PageHeader.Lsn = lsn;

        return new ChangeSpan(HeaderLsnOffset,
                              LogSequenceNumber.Size,
                              $"Page header LSN set to {lsn.ToBinaryString()}")
        {
            ItemType = ItemType.Lsn,
            Value = lsn.ToBinaryString()
        };
    }

    protected static void SetSlotCount(PageData page, ushort value, List<ChangeSpan> changes)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(page.Data.AsSpan(SlotCountOffset), value);

        page.PageHeader.SlotCount = value;

        changes.Add(new ChangeSpan(SlotCountOffset, sizeof(ushort), $"Page header slot count set to {value}")
        {
            ItemType = ItemType.SlotCount,
            Value = value.ToString()
        });
    }

    protected static void SetFreeCount(PageData page, ushort value, List<ChangeSpan> changes)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(page.Data.AsSpan(FreeCountOffset), value);

        page.PageHeader.FreeCount = value;

        changes.Add(new ChangeSpan(FreeCountOffset, sizeof(ushort), $"Page header free count set to {value}")
        {
            ItemType = ItemType.FreeCount,
            Value = value.ToString()
        });
    }

    protected static void SetFreeData(PageData page, ushort value, List<ChangeSpan> changes)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(page.Data.AsSpan(FreeDataOffset), value);

        page.PageHeader.FreeData = value;

        changes.Add(new ChangeSpan(FreeDataOffset, sizeof(ushort), $"Page header free data offset set to {value}")
        {
            ItemType = ItemType.FreeDataOffset,
            Value = value.ToString()
        });
    }

    protected static void SetGhostCount(PageData page, short value, List<ChangeSpan> changes)
    {
        BinaryPrimitives.WriteInt16LittleEndian(page.Data.AsSpan(GhostCountOffset), value);

        page.PageHeader.GhostRecordCount = value;

        changes.Add(new ChangeSpan(GhostCountOffset, sizeof(short), $"Page header ghost record count set to {value}")
        {
            ItemType = ItemType.GhostRecordCount,
            Value = value.ToString()
        });
    }

    protected static int GetOffsetTableEntryPosition(int slotId)
    {
        return PageData.Size - 2 * (slotId + 1);
    }

    /// <summary>
    /// Writes an offset table entry into the page image
    /// </summary>
    /// <remarks>
    /// The offset table grows backwards from the end of the page - entry n is the 2 bytes at Size - 2 * (n + 1)
    /// </remarks>
    protected static void WriteOffsetTableEntry(PageData page, int slotId, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(page.Data.AsSpan(GetOffsetTableEntryPosition(slotId)), value);
    }

    /// <summary>
    /// Rebuilds the parsed offset table from the page image
    /// </summary>
    protected static void RebuildOffsetTable(PageData page)
    {
        var offsetTable = new ushort[page.PageHeader.SlotCount];

        for (var slotId = 0; slotId < offsetTable.Length; slotId++)
        {
            offsetTable[slotId] =
                BinaryPrimitives.ReadUInt16LittleEndian(page.Data.AsSpan(PageData.Size - 2 * (slotId + 1)));
        }

        page.OffsetTable = offsetTable;
    }

    /// <summary>
    /// Calculates the length of a FixedVar record from its bytes alone
    /// </summary>
    /// <remarks>
    /// Follows the record structure - status bits, fixed length size, column count, null bitmap, variable count and variable end offset
    /// array - so no table metadata is needed.
    ///
    /// The record end is the last variable column's end offset (masked of the 0x8000 complex column flag), or the end of the null bitmap
    /// / variable count for records with no variable data.
    ///
    /// Only primary records are supported - forwarding stubs, ghosts and compressed records return false.
    /// </remarks>
    protected static bool TryGetRowLength(byte[] data, int rowOffset, out int length)
    {
        length = 0;

        var page = data.AsSpan();

        if (rowOffset < 0 || rowOffset + 4 > page.Length)
        {
            return false;
        }

        var statusA = page[rowOffset];

        var recordType = (statusA >> 1) & 0x7;

        if (recordType != 0)
        {
            return false;
        }

        var hasNullBitmap = (statusA & 0x10) != 0;

        var hasVariableColumns = (statusA & 0x20) != 0;

        var columnCountPosition = rowOffset + BinaryPrimitives.ReadUInt16LittleEndian(page[(rowOffset + 2)..]);

        if (columnCountPosition + 2 > page.Length)
        {
            return false;
        }

        var columnCount = BinaryPrimitives.ReadUInt16LittleEndian(page[columnCountPosition..]);

        var position = columnCountPosition + 2;

        if (hasNullBitmap)
        {
            position += (columnCount + 7) / 8;
        }

        if (!hasVariableColumns)
        {
            length = position - rowOffset;

            return length > 0 && rowOffset + length <= page.Length;
        }

        if (position + 2 > page.Length)
        {
            return false;
        }

        var variableCount = BinaryPrimitives.ReadUInt16LittleEndian(page[position..]);

        position += 2;

        if (variableCount == 0)
        {
            length = position - rowOffset;

            return rowOffset + length <= page.Length;
        }

        var lastOffsetPosition = position + 2 * (variableCount - 1);

        if (lastOffsetPosition + 2 > page.Length)
        {
            return false;
        }

        length = BinaryPrimitives.ReadUInt16LittleEndian(page[lastOffsetPosition..]) & 0x7FFF;

        return length > 0 && rowOffset + length <= page.Length;
    }

    /// <summary>
    /// Writes a rebuilt row back to the page, in place or relocated to the free data offset
    /// </summary>
    /// <remarks>
    /// A shrinking row is rewritten in place leaving its old tail as a hole; a growing row is extended in place when it is the last row
    /// before the free data offset, otherwise it is relocated to the free data offset (rows always land at free data) and the slot's
    /// offset table entry is re-pointed, leaving the old copy as a hole.
    ///
    /// Free data and free count accounting is updated to match.
    /// </remarks>
    protected static ApplyResult PlaceRebuiltRow(PageData page,
                                                 int slotId,
                                                 int rowOffset,
                                                 int oldLength,
                                                 byte[] newRow,
                                                 int firstChangeOffset,
                                                 string description,
                                                 List<ChangeSpan> changes)
    {
        var delta = newRow.Length - oldLength;

        var header = page.PageHeader;

        var offsetTableStart = PageData.Size - 2 * header.SlotCount;

        var isLastRow = rowOffset + oldLength == header.FreeData;

        if (delta < 0 || isLastRow)
        {
            if (isLastRow && rowOffset + newRow.Length > offsetTableStart)
            {
                return new ApplyResult(ApplyStatus.NotSupported,
                                       "Not enough contiguous free space (page needs compaction)");
            }

            newRow.CopyTo(page.Data.AsSpan(rowOffset));

            changes.Add(new ChangeSpan(rowOffset + firstChangeOffset,
                                       newRow.Length - firstChangeOffset,
                                       description));

            if (isLastRow)
            {
                SetFreeData(page, (ushort)(rowOffset + newRow.Length), changes);
            }
        }
        else
        {
            if (header.FreeData + newRow.Length > offsetTableStart)
            {
                return new ApplyResult(ApplyStatus.NotSupported,
                                       "Not enough contiguous free space (page needs compaction)");
            }

            var newOffset = header.FreeData;

            newRow.CopyTo(page.Data.AsSpan(newOffset));

            WriteOffsetTableEntry(page, slotId, (ushort)newOffset);

            changes.Add(new ChangeSpan(newOffset,
                                       newRow.Length,
                                       $"{description} (row relocated from offset {rowOffset})"));

            changes.Add(new ChangeSpan(GetOffsetTableEntryPosition(slotId),
                                       sizeof(ushort),
                                       $"Slot offset table entry {slotId} set to {newOffset}"));

            SetFreeData(page, (ushort)(newOffset + newRow.Length), changes);

            RebuildOffsetTable(page);
        }

        SetFreeCount(page, (ushort)(header.FreeCount - delta), changes);

        return ApplyResult.Applied(changes);
    }

    protected static bool TryGetSlotOffset(PageData page, int slotId, out int offset)
    {
        if (slotId < 0 || slotId >= page.OffsetTable.Length)
        {
            offset = 0;

            return false;
        }

        offset = page.OffsetTable[slotId];

        return true;
    }

    /// <summary>
    /// Replaces a byte range in the page image, verifying the before image when one is present
    /// </summary>
    /// <remarks>
    /// Same-size splices only - a splice that changes the range's length changes the row's footprint, which needs the row rebuild/relocate
    /// page surgery that is not implemented yet.
    /// </remarks>
    protected static ApplyResult ApplySplice(PageData page,
                                             int offset,
                                             int size,
                                             byte[] before,
                                             byte[] after,
                                             string description)
    {
        if (after.Length != size)
        {
            return new ApplyResult(ApplyStatus.NotSupported,
                                   $"Size-changing splice ({size} -> {after.Length} bytes) is not supported");
        }

        if (offset + size > page.Data.Length)
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Splice at {offset} overruns the page");
        }

        var target = page.Data.AsSpan(offset, size);

        if (before.Length > 0 && !target.SequenceEqual(before))
        {
            return new ApplyResult(ApplyStatus.BeforeImageMismatch,
                                   $"Page bytes at {offset} do not match the record's before image");
        }

        after.CopyTo(target);

        return ApplyResult.Applied([new ChangeSpan(offset, size, description)]);
    }
}

/// <summary>
/// Base class for appliers handling a specific log record type
/// </summary>
/// <remarks>
/// Runs the guards common to every page scoped record - the record must target the page and the page header LSN must match the record's
/// PreviousPageLsn - then hands over to the type specific ApplyRecord, stamping the record's LSN into the page header if it applied.
/// </remarks>
public abstract class PageLogRecordApplier<TRecord> : PageLogRecordApplier
    where TRecord : PageLogRecord
{
    public ApplyResult Apply(PageData page, TRecord record)
    {
        if (record.PageAddress != page.PageAddress)
        {
            return new ApplyResult(ApplyStatus.PageMismatch,
                                   $"Record targets {record.PageAddress}, page is {page.PageAddress}");
        }

        if (record.PreviousPageLsn != page.PageHeader.Lsn)
        {
            return new ApplyResult(ApplyStatus.LsnMismatch,
                                   $"Record {record.Lsn.ToBinaryString()} expects page LSN " +
                                   $"{record.PreviousPageLsn.ToBinaryString()} but page is at " +
                                   $"{page.PageHeader.Lsn.ToBinaryString()}");
        }

        var result = ApplyRecord(page, record);

        if (result.IsApplied)
        {
            var lsnChange = StampLsn(page, record.Lsn);

            result = result with { Changes = [.. result.Changes, lsnChange] };
        }

        return result;
    }

    protected abstract ApplyResult ApplyRecord(PageData page, TRecord record);
}
