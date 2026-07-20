using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_MODIFY_HEADER log records
/// </summary>
/// <remarks>
/// Splices the after image over the page header at the record's header offset, verifying the before image. Used on
/// PFS pages (and others) to update header fields such as m_typeFlagBits.
/// </remarks>
public sealed class ModifyHeaderApplier : PageLogRecordApplier<ModifyHeaderLogRecord>
{
    private const int PageHeaderSize = 96;

    protected override ApplyResult ApplyRecord(PageData page, ModifyHeaderLogRecord record)
    {
        if (record.HeaderOffset + record.AfterData.Length > PageHeaderSize)
        {
            return new ApplyResult(ApplyStatus.NotSupported,
                                   $"Header modification at offset {record.HeaderOffset} is beyond the page header");
        }

        return ApplySplice(page,
                           record.HeaderOffset,
                           record.AfterData.Length,
                           record.BeforeData,
                           record.AfterData,
                           $"Page header data modified at offset {record.HeaderOffset}");
    }
}
