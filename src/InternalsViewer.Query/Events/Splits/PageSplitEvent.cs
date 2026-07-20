using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Events.Splits;

/// <summary>
/// Page split event
/// </summary>
/// <remarks>
/// Page splits occur for:
/// 
/// - B-tree splits
/// - New page allocations (SPLIT_FOR_NEW_PAGE)
///
/// Different split operations are recorded in the event's SplitOperation property, and the new page allocated by the split is in NewPage.
/// </remarks>
public sealed record PageSplitEvent : PageEngineEvent
{
    public PageSplitOperation SplitOperation { get; init; }

    /// <summary>
    /// Page added to the structure by the split/allocation
    /// </summary>
    public PageAddress? NewPage { get; init; }

    public long RowsetId { get; init; }

    /// <summary>
    /// Log records forming the split's footprint, attached by the page split matcher
    /// </summary>
    /// <remarks>
    /// The records targeting the splitting and new pages, plus the rest of the system transaction that formatted the new page
    /// (allocation bitmap and PFS maintenance, root/IAM changes), in LSN order
    /// </remarks>
    public List<LogRecord> LogRecords { get; set; } = [];

    public override string Description => NewPage is { } newPage
        ? $"{SplitOperation} -> {newPage}"
        : $"{SplitOperation}";
}
