using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_SET_FREE_SPACE log records
/// </summary>
/// <remarks>
/// Rewrites the single PFS byte for the tracked page, verifying the page image holds the record's old value first
/// </remarks>
public sealed class SetFreeSpaceApplier : PageLogRecordApplier<SetFreeSpaceLogRecord>
{
    internal const int PfsByteArrayOffset = 100;

    protected override ApplyResult ApplyRecord(PageData page, SetFreeSpaceLogRecord record)
    {
        var offset = PfsByteArrayOffset + record.PageOffset;

        if (page.Data[offset] != record.OldValue)
        {
            return new ApplyResult(ApplyStatus.BeforeImageMismatch,
                                   $"PFS byte at {offset} is 0x{page.Data[offset]:X2}, " +
                                   $"record expects 0x{record.OldValue:X2}");
        }

        page.Data[offset] = record.NewValue;

        var trackedPage = record.PageAddress.PageId + record.PageOffset;

        return ApplyResult.Applied(
        [
            new ChangeSpan(offset,
                           sizeof(byte),
                           $"PFS byte for page ({record.PageAddress.FileId}:{trackedPage}) changed " +
                           $"0x{record.OldValue:X2} -> 0x{record.NewValue:X2}")
        ]);
    }
}
