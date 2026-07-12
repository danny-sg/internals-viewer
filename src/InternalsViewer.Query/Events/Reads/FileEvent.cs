using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Events.Reads;

public sealed record FileEvent : PageEngineEvent
{
    public bool IsRead { get; init; }

    public ReadMode Mode { get; set; } 

    public short FileId { get; set; }

    public long Offset { get; set; }

    public long Size { get; set; }

    public PageAddress FromPageAddress => new PageAddress(FileId, (int)Offset / 8192);

    public PageAddress ToPageAddress => new PageAddress(FileId, (int)(Offset + Size) / 8192);

    public override string Description => $"File {(IsRead ? "Read" : "Write")} {PageAddress}";

    public override string Detail
    {
        get
        {
            if (string.IsNullOrEmpty(ObjectName))
            {
                return $"File {(IsRead ? "Read" : "Write")}: {PageAddress}";
            }

            return $"File {(IsRead ? "Read" : "Write")}: {PageAddress} {ObjectName}";
        }
    }
}