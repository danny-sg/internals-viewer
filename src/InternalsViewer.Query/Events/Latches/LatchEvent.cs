using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Events.Latches;

[EventItemName("Latch")]
public sealed record LatchEvent : PageEngineEvent
{
    public LatchMode LatchMode { get; init; }

    public LatchClass LatchClass { get; init; }

    public override string Description => $"Latch: {LatchClass} {LatchMode} - {PageAddress}";

    public override string Name => "Latch";

    public override string Detail
    {
        get
        {
            var name = EventItemName.Get(GetType());

            var latchClassName = EventItemName.Get(LatchClass);

            var latchModeName = EventItemName.Get(LatchMode);

            if (!string.IsNullOrEmpty(latchModeName))
            {
                latchModeName = $" ({latchModeName})";
            }

            if(string.IsNullOrEmpty(ObjectName))
            {
                return $"{latchClassName} {name}: {PageAddress}";
            }

            return $"{latchClassName} {name}{latchModeName}: {PageAddress} - {ObjectName}";
        }
    }

    public ulong? LatchAddress { get; set; }
}