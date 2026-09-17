using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Events.Latches;

[EventItemName("Latch")]
public sealed partial record LatchEvent : PageEngineEvent
{
    [EventProperty("Mode")]
    public LatchMode LatchMode { get; init; }

    [EventProperty("Latch Class")]
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

    [EventProperty("Latch Address", Type = EventPropertyType.Address)]
    public ulong? LatchAddress { get; set; }
}