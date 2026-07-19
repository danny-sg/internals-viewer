using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog.Appliers;

/// <summary>
/// Applier for LOP_MODIFY_COLUMNS log records
/// </summary>
/// <remarks>
/// Applies each modification region as a splice against the row
/// </remarks>
public sealed class ModifyColumnsApplier : PageLogRecordApplier<ModifyColumnsLogRecord>
{
    protected override ApplyResult ApplyRecord(PageData page, ModifyColumnsLogRecord record)
    {
        if (!TryGetSlotOffset(page, record.SlotId, out var rowOffset))
        {
            return new ApplyResult(ApplyStatus.NotSupported, $"Slot {record.SlotId} is not in the offset table");
        }

        foreach (var modification in record.Modifications)
        {
            if (modification.BeforeData.Length != modification.AfterData.Length)
            {
                return new ApplyResult(ApplyStatus.NotSupported, "Size-changing modification is not supported");
            }
        }

        var changes = new List<ChangeSpan>();

        foreach (var modification in record.Modifications)
        {
            var result = ApplySplice(page,
                                     rowOffset + modification.AfterOffset,
                                     modification.BeforeData.Length,
                                     modification.BeforeData,
                                     modification.AfterData,
                                     $"Slot {record.SlotId} row modified at row offset {modification.AfterOffset}");

            if (!result.IsApplied)
            {
                return result;
            }

            changes.AddRange(result.Changes);
        }

        return ApplyResult.Applied(changes);
    }
}
