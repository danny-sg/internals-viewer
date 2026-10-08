using InternalsViewer.Query.Events.Properties;

namespace InternalsViewer.Query.Events.BatchMode;

public sealed partial record SegmentEliminateEvent : EngineEvent
{
    [EventProperty("Row Group")]
    public long RowGroupId { get; set; }

    [EventProperty("Hobt Id")]
    public ulong HobtId { get; set; }

    [EventProperty("Unique Value Filter")]
    public bool IsEliminatedByUniqueValueFilter { get; set; }

    public override string Name => "Segment Eliminated";

    public override string Description => $"Segment Eliminated (Row Group {RowGroupId})";

    public override string Detail
        => $"{Description} - {(IsEliminatedByUniqueValueFilter ? "Eliminated by unique value filter" : "Eliminated by segment metadata")}";
}
