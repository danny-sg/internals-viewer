using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Events.EventTypes;

[EventItemName("Latch")]
public sealed record LatchEvent : EngineEvent
{
    public LatchMode LatchMode { get; init; }

    public LatchClass LatchClass { get; init; }

    public override string Description => $"Latch: {LatchClass} {LatchMode} - {PageAddress}";

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
}