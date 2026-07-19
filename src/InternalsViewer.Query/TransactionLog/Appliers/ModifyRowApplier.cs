using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_MODIFY_ROW log records
/// </summary>
public sealed class ModifyRowApplier : PageLogRecordApplier<ModifyRowLogRecord>
{
    protected override ApplyResult ApplyRecord(PageData page, ModifyRowLogRecord record)
    {
        if (!TryGetSlotOffset(page, record.SlotId, out var rowOffset))
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Slot {record.SlotId} is not in the offset table");
        }

        return ApplySplice(page,
                           rowOffset + record.OffsetInRow,
                           record.ModifySize,
                           record.BeforeData,
                           record.AfterData,
                           $"Slot {record.SlotId} row modified at row offset {record.OffsetInRow}");
    }
}
