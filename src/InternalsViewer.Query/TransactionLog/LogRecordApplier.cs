using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog;

/// <summary>
/// Applies page scoped log records to a page image, rolling the page forward from a captured initial state
/// </summary>
/// <remarks>
/// Redo only - the capture mechanism runs the query in a transaction and rolls back, so the log ends with
/// compensation records and replaying every record forward returns the page to its initial state. Each apply
/// verifies the page header LSN matches the record's PreviousPageLsn and stamps the record's own LSN afterwards,
/// so a missing or foreign record surfaces as an LsnMismatch instead of a silently wrong image.
/// </remarks>
public static class LogRecordApplier
{
    private const int HeaderLsnOffset = 40;

    private const int PfsByteArrayOffset = 100;

    private const int AllocationBitmapPrefixBits = 32;

    public static ApplyResult Apply(PageData page, PageLogRecord record)
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

        var result = record switch
        {
            ModifyRowLogRecord modifyRow => ApplyModifyRow(page, modifyRow),
            ModifyColumnsLogRecord modifyColumns => ApplyModifyColumns(page, modifyColumns),
            SetFreeSpaceLogRecord setFreeSpace => ApplySetFreeSpace(page, setFreeSpace),
            SetBitsLogRecord setBits => ApplySetBits(page, setBits),
            _ => new ApplyResult(ApplyStatus.NotSupported, $"{record.Operation} is not supported")
        };

        if (result.IsApplied)
        {
            StampLsn(page, record.Lsn);
        }

        return result;
    }

    public static ApplyResult Replay(PageData page, IEnumerable<PageLogRecord> records, LogSequenceNumber upTo)
    {
        var pageRecords = records.Where(r => r.PageAddress == page.PageAddress)
                                 .OrderBy(r => (r.Lsn.VirtualLogFile, r.Lsn.FileOffset, r.Lsn.RecordSequence));

        foreach (var record in pageRecords)
        {
            if ((record.Lsn.VirtualLogFile, record.Lsn.FileOffset, record.Lsn.RecordSequence)
                    .CompareTo((upTo.VirtualLogFile, upTo.FileOffset, upTo.RecordSequence)) > 0)
            {
                break;
            }

            var result = Apply(page, record);

            if (!result.IsApplied)
            {
                return result with { Message = $"{record.Lsn.ToBinaryString()}: {result.Message}" };
            }
        }

        return ApplyResult.Success;
    }

    private static ApplyResult ApplyModifyRow(PageData page, ModifyRowLogRecord record)
    {
        if (record.SlotId < 0 || record.SlotId >= page.OffsetTable.Length)
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Slot {record.SlotId} is not in the offset table");
        }

        return ApplySplice(page,
                           page.OffsetTable[record.SlotId] + record.OffsetInRow,
                           record.ModifySize,
                           record.BeforeData,
                           record.AfterData);
    }

    private static ApplyResult ApplyModifyColumns(PageData page, ModifyColumnsLogRecord record)
    {
        if (record.SlotId < 0 || record.SlotId >= page.OffsetTable.Length)
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Slot {record.SlotId} is not in the offset table");
        }

        var rowOffset = page.OffsetTable[record.SlotId];

        foreach (var modification in record.Modifications)
        {
            if (modification.BeforeData.Length != modification.AfterData.Length)
            {
                return new ApplyResult(ApplyStatus.NotSupported, "Size-changing modification is not supported");
            }
        }

        foreach (var modification in record.Modifications)
        {
            var result = ApplySplice(page,
                                     rowOffset + modification.AfterOffset,
                                     modification.BeforeData.Length,
                                     modification.BeforeData,
                                     modification.AfterData);

            if (!result.IsApplied)
            {
                return result;
            }
        }

        return ApplyResult.Success;
    }

    private static ApplyResult ApplySplice(PageData page, int offset, int size, byte[] before, byte[] after)
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

        return ApplyResult.Success;
    }

    private static ApplyResult ApplySetFreeSpace(PageData page, SetFreeSpaceLogRecord record)
    {
        var offset = PfsByteArrayOffset + record.PageOffset;

        if (page.Data[offset] != record.OldValue)
        {
            return new ApplyResult(ApplyStatus.BeforeImageMismatch,
                                   $"PFS byte at {offset} is 0x{page.Data[offset]:X2}, " +
                                   $"record expects 0x{record.OldValue:X2}");
        }

        page.Data[offset] = record.NewValue;

        return ApplyResult.Success;
    }

    private static ApplyResult ApplySetBits(PageData page, SetBitsLogRecord record)
    {
        if (record.Context == LogContext.PFS)
        {
            return new ApplyResult(ApplyStatus.NotSupported, "SET_BITS against a PFS page is not supported");
        }

        var firstBit = record.FirstBit - AllocationBitmapPrefixBits;

        if (firstBit < 0)
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Bit {record.FirstBit} is inside the bitmap prefix");
        }

        for (var i = 0; i < record.BitCount; i++)
        {
            var bit = firstBit + i;

            var offset = AllocationPage.AllocationArrayOffset + bit / 8;

            var mask = (byte)(1 << (bit % 8));

            if (record.BitValue == 0)
            {
                page.Data[offset] &= (byte)~mask;
            }
            else
            {
                page.Data[offset] |= mask;
            }
        }

        return ApplyResult.Success;
    }

    /// <summary>
    /// Stamps a page image with the LSN a replay chain starts from
    /// </summary>
    /// <remarks>
    /// The capture mechanism rolls the transaction back, so a freshly loaded page holds the LSN of the last
    /// compensation record rather than the pre-transaction LSN the first captured record expects. The content is
    /// identical, so rebasing the header LSN to the first record's PreviousPageLsn makes the baseline replayable.
    /// </remarks>
    public static void Rebase(PageData page, LogSequenceNumber lsn)
    {
        StampLsn(page, lsn);
    }

    private static void StampLsn(PageData page, LogSequenceNumber lsn)
    {
        var span = page.Data.AsSpan(HeaderLsnOffset);

        BinaryPrimitives.WriteInt32LittleEndian(span, lsn.VirtualLogFile);
        BinaryPrimitives.WriteInt32LittleEndian(span[sizeof(int)..], lsn.FileOffset);
        BinaryPrimitives.WriteInt16LittleEndian(span[(2 * sizeof(int))..], lsn.RecordSequence);

        page.PageHeader.Lsn = lsn;
    }
}
