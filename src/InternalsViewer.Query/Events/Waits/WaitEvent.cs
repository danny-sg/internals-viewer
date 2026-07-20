namespace InternalsViewer.Query.Events.Waits;

/// <summary>
/// A wait recorded against a task
/// </summary>
/// <remarks>
/// A page IO wait carries the page it was waiting on, which it takes from the latch suspend that measures it — see
/// <see cref="Consolidation.WaitAligner"/>. Other wait types have no page and leave it null.
/// </remarks>
public sealed record WaitEvent : PageEngineEvent
{
    public WaitType WaitType { get; set; }

    /// <summary>
    /// The resource the task is waiting on, which for a page IO wait is the address of the BUF latch
    /// </summary>
    public ulong? WaitResource { get; set; }

    public override string Description => $"Wait: {WaitType}";

    public bool IsEnd { get; set; }
}