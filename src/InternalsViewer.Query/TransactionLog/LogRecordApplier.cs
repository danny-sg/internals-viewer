using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.Appliers;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog;

/// <summary>
/// Applies page scoped log records to a page image, rolling the page forward from a captured initial state
/// </summary>
/// <remarks>
/// Redo only - the capture mechanism runs the query in a transaction and rolls back, so the log ends with compensation records and
/// replaying every record forward returns the page to its initial state. Dispatches each record to the applier for its type. The shared
/// guards and LSN stamping live in the applier base class.
/// </remarks>
public static class LogRecordApplier
{
    private static readonly ModifyRowApplier ModifyRowApplier = new();

    private static readonly ModifyColumnsApplier ModifyColumnsApplier = new();

    private static readonly SetFreeSpaceApplier SetFreeSpaceApplier = new();

    private static readonly SetBitsApplier SetBitsApplier = new();

    private static readonly InsertRowsApplier InsertRowsApplier = new();

    private static readonly DeleteRowsApplier DeleteRowsApplier = new();

    public static ApplyResult Apply(PageData page, PageLogRecord record)
    {
        return record switch
        {
            ModifyRowLogRecord modifyRow 
                => ModifyRowApplier.Apply(page, modifyRow),
            ModifyColumnsLogRecord modifyColumns 
                => ModifyColumnsApplier.Apply(page, modifyColumns),
            SetFreeSpaceLogRecord setFreeSpace 
                => SetFreeSpaceApplier.Apply(page, setFreeSpace),
            SetBitsLogRecord setBits
                => SetBitsApplier.Apply(page, setBits),
            InsertRowsLogRecord insertRows
                => InsertRowsApplier.Apply(page, insertRows),
            DeleteRowsLogRecord deleteRows
                => DeleteRowsApplier.Apply(page, deleteRows),
            _ => new ApplyResult(ApplyStatus.NotSupported, $"{record.Operation} is not supported")
        };
    }

    /// <summary>
    /// Replays the page's records in LSN order up to and including the target LSN
    /// </summary>
    /// <remarks>
    /// The returned result carries the change spans of the last record applied, so a caller replaying to a selected record can highlight
    /// what that record changed
    /// </remarks>
    public static ApplyResult Replay(PageData page, IEnumerable<PageLogRecord> records, LogSequenceNumber upTo)
    {
        var pageRecords = records.Where(r => r.PageAddress == page.PageAddress)
                                 .OrderBy(r => (r.Lsn.VirtualLogFile, r.Lsn.FileOffset, r.Lsn.RecordSequence));

        var lastApplied = ApplyResult.Success;

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

            lastApplied = result;
        }

        return lastApplied;
    }

    /// <summary>
    /// Stamps a page image with the LSN a replay chain starts from
    /// </summary>
    /// <remarks>
    /// The capture mechanism rolls the transaction back, so a freshly loaded page holds the LSN of the last compensation record rather
    /// than the pre-transaction LSN the first captured record expects. The content is identical, so rebasing the header LSN to the first
    /// record's PreviousPageLsn makes the baseline replayable.
    /// </remarks>
    public static void Rebase(PageData page, LogSequenceNumber lsn)
    {
        PageLogRecordApplier.StampLsn(page, lsn);
    }
}
