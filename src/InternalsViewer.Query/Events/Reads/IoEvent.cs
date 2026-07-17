namespace InternalsViewer.Query.Events.Reads;

/// <summary>
/// IO (page read/write) event
/// </summary>
public sealed record IoEvent : PageEngineEvent
{
    public bool IsRead { get; init; }

    public override string Description => $"Page {(IsRead ? "Read" : "Write")} {PageAddress}";

    public bool IsRoot { get; set; }

    public override string Detail
    {
        get
        {
            if (string.IsNullOrEmpty(ObjectName))
            {
                return $"Page {(IsRead ? "Read" : "Write")}: {PageAddress}";
            }

            return $"Page {(IsRead ? "Read" : "Write")}: {PageAddress} {ObjectName}";
        }
    }
}