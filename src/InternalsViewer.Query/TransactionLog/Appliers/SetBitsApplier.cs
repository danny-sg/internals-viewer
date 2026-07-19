using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_SET_BITS log records
/// </summary>
/// <remarks>
/// Fills the run [FirstBit, FirstBit + BitCount) in the page's allocation bitmap with BitValue. FirstBit counts from the start of the
/// bitmap record's data including its 32 bit prefix, so the page offset works out as AllocationArrayOffset + (FirstBit - 32) / 8.
/// </remarks>
public sealed class SetBitsApplier : PageLogRecordApplier<SetBitsLogRecord>
{
    private const int BitmapPrefixBits = 32;

    protected override ApplyResult ApplyRecord(PageData page, SetBitsLogRecord record)
    {
        if (record.Context == LogContext.PFS)
        {
            return new ApplyResult(ApplyStatus.NotSupported, "SET_BITS against a PFS page is not supported");
        }

        var firstBit = record.FirstBit - BitmapPrefixBits;

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

        var startOffset = AllocationPage.AllocationArrayOffset + firstBit / 8;

        var endOffset = AllocationPage.AllocationArrayOffset + (firstBit + record.BitCount - 1) / 8;

        return ApplyResult.Applied(
        [
            new ChangeSpan(startOffset,
                           endOffset - startOffset + 1,
                           $"{record.Context} bitmap: {record.BitCount} bit(s) from bit {firstBit} " +
                           $"set to {record.BitValue}")
        ]);
    }
}
