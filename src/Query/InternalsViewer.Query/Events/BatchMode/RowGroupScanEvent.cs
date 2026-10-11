using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.BatchMode;

public sealed partial record RowGroupScanEvent : EngineEvent, IEventGroup
{
    public required IReadOnlyList<EngineEvent> Events { get; init; }

    [EventProperty("Row Group")]
    public long RowGroupId { get; init; }

    public override string Name => "Rowgroup Scan";

    public override string Description => $"Rowgroup Scan (Row Group {RowGroupId}, {Events.Count:N0} events)";
}
