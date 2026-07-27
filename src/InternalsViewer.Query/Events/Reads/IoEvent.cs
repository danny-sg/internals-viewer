namespace InternalsViewer.Query.Events.Reads;

/// <summary>
/// IO (page read/write) event
/// </summary>
public sealed record IoEvent : PageEngineEvent
{
    public bool IsRead { get; init; }

    public override string Description
    {
        get
        {
            var description = $"Page {(IsRead ? "Read" : "Write")} {PageAddress}";

            if (PageAddress == AllocationUnit?.FirstIamPage)
            {
                return $"{description} - First IAM Page";
            }

            if (PageAddress == AllocationUnit?.RootPage)
            {
                return $"{description} - Root Page";
            }

            return description;
        }
    }

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