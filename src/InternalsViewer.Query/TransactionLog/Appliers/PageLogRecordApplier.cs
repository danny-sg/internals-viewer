using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog.Appliers;

/// <summary>
/// Base class providing the page image operations shared by the log record appliers
/// </summary>
public abstract class PageLogRecordApplier
{
    private const int SlotCountOffset = 22;

    private const int FreeCountOffset = 28;

    private const int FreeDataOffset = 30;

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
                              $"Page header LSN set to {lsn.ToBinaryString()}");
    }

    protected static void SetSlotCount(PageData page, ushort value, List<ChangeSpan> changes)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(page.Data.AsSpan(SlotCountOffset), value);

        page.PageHeader.SlotCount = value;

        changes.Add(new ChangeSpan(SlotCountOffset, sizeof(ushort), $"Page header slot count set to {value}"));
    }

    protected static void SetFreeCount(PageData page, ushort value, List<ChangeSpan> changes)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(page.Data.AsSpan(FreeCountOffset), value);

        page.PageHeader.FreeCount = value;

        changes.Add(new ChangeSpan(FreeCountOffset, sizeof(ushort), $"Page header free count set to {value}"));
    }

    protected static void SetFreeData(PageData page, ushort value, List<ChangeSpan> changes)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(page.Data.AsSpan(FreeDataOffset), value);

        page.PageHeader.FreeData = value;

        changes.Add(new ChangeSpan(FreeDataOffset, sizeof(ushort), $"Page header free data offset set to {value}"));
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
    /// Rebuilds the parsed offset table from the page image after a structural change
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
