using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events.Reads;

/// <summary>
/// File Read/Write event
/// </summary>
public sealed partial record FileEvent : PageEngineEvent
{
    [EventProperty("Read")]
    public bool IsRead { get; init; }

    [EventProperty("Mode")]
    public ReadMode Mode { get; set; }

    [EventProperty("File Id")]
    public short FileId { get; set; }

    [EventProperty("Offset", Type = EventPropertyType.Bytes)]
    public long Offset { get; set; }

    [EventProperty("Size", Type = EventPropertyType.Bytes)]
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