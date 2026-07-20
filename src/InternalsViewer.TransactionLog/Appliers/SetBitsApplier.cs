using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_SET_BITS log records
/// </summary>
/// <remarks>
/// Fills the run [FirstBit, FirstBit + BitCount) with BitValue. FirstBit counts from the start of the bitmap
/// record's data including its 32 bit prefix. For an allocation page the bits index the allocation bitmap
/// (AllocationArrayOffset); for a PFS page they index the PFS byte array (PfsByteArrayOffset), one flag bit per
/// tracked page - e.g. the ghost bit.
/// </remarks>
public sealed class SetBitsApplier : PageLogRecordApplier<SetBitsLogRecord>
{
    private const int BitmapPrefixBits = 32;

    private const int PfsByteArrayOffset = 100;

    protected override ApplyResult ApplyRecord(PageData page, SetBitsLogRecord record)
    {
        var firstBit = record.FirstBit - BitmapPrefixBits;

        if (firstBit < 0)
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Bit {record.FirstBit} is inside the bitmap prefix");
        }

        var arrayOffset = record.Context == LogContext.PFS
            ? PfsByteArrayOffset
            : AllocationPage.AllocationArrayOffset;

        for (var i = 0; i < record.BitCount; i++)
        {
            var bit = firstBit + i;

            var offset = arrayOffset + bit / 8;

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

        var startOffset = arrayOffset + firstBit / 8;

        var endOffset = arrayOffset + (firstBit + record.BitCount - 1) / 8;

        var description = record.Context == LogContext.PFS
            ? $"PFS flag bit for page offset {firstBit / 8} set to {record.BitValue}"
            : $"{record.Context} bitmap: {record.BitCount} bit(s) from bit {firstBit} set to {record.BitValue}";

        return ApplyResult.Applied([new ChangeSpan(startOffset, endOffset - startOffset + 1, description)]);
    }
}
