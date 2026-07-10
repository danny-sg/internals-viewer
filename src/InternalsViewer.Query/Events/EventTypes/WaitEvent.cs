using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Locks;

namespace InternalsViewer.Query.Events.EventTypes;

public sealed record WaitEvent : EngineEvent
{
    public WaitType WaitType { get; set; }

    public LatchEvent? LatchEvent { get; set; }

    public override long DurationUs
    {
        get
        {
            return LatchEvent?.DurationUs ?? base.DurationUs;
        }
        set
        {
            base.DurationUs = value;
        }
    }

    public override PageAddress? PageAddress
    {
        get
        {
            return LatchEvent?.PageAddress ?? base.PageAddress;
        }
        set
        {
            base.PageAddress = value;
        }
    }

    public override string Description => $"Wait: {WaitType}";
}