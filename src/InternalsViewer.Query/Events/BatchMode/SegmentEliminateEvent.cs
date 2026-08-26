namespace InternalsViewer.Query.Events.BatchMode;

public sealed record SegmentEliminateEvent : EngineEvent
{
    public long RowGroupId { get; set; }

    public ulong HobtId { get; set; }

    public bool IsEliminatedByUniqueValueFilter { get; set; }

    public override string Description => $"Segment Eliminated (Row Group {RowGroupId})";

    public override string Detail
        => IsEliminatedByUniqueValueFilter ? "Eliminated by unique value filter" : "Eliminated by segment metadata";
}
