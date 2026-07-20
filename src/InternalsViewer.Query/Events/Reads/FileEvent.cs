using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events.Reads;

/// <summary>
/// File Read/Write event
/// </summary>
public sealed record FileEvent : PageEngineEvent
{
    public bool IsRead { get; init; }

    public ReadMode Mode { get; set; }

    public short FileId { get; set; }

    public long Offset { get; set; }

    public long Size { get; set; }

    public PageAddress FromPageAddress => new(FileId, (int)(Offset / 8192));

    public PageAddress ToPageAddress => new(FileId, (int)((Offset + Size) / 8192));

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