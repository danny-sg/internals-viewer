using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Events.Splits;

/// <summary>
/// Attaches the log records forming a page split's footprint to its page split event
/// </summary>
/// <remarks>
/// A split (or new page allocation) runs as nested system transactions, so the footprint is gathered two ways:
/// records directly targeting the splitting page or the new page, plus every record in the system transactions
/// that formatted the new page - which pulls in the allocation bitmap, PFS and root/IAM maintenance records that
/// target other pages.
/// </remarks>
internal class PageSplitEventMatcher
{
    public static void Match(List<EngineEvent> events, List<LogRecord> logRecords)
    {
        foreach (var engineEvent in events)
        {
            if (engineEvent is not PageSplitEvent splitEvent)
            {
                continue;
            }

            var formatTransactionIds = logRecords.OfType<FormatPageLogRecord>()
                                                 .Where(f => f.PageAddress == splitEvent.NewPage)
                                                 .Select(f => f.TransactionId)
                                                 .Where(t => t is > 0)
                                                 .ToHashSet();

            splitEvent.LogRecords = logRecords.Where(r => (r is PageLogRecord pageRecord
                                                         && (pageRecord.PageAddress == splitEvent.PageAddress
                                                             || pageRecord.PageAddress == splitEvent.NewPage))
                                                        || (r.TransactionId is > 0 && formatTransactionIds.Contains(r.TransactionId)))
                                              .OrderBy(r => (r.Lsn.VirtualLogFile, r.Lsn.FileOffset, r.Lsn.RecordSequence))
                                              .ToList();
        }
    }
}
