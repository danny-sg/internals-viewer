namespace InternalsViewer.Replay.Plans;

public class OperatorExecution
{
    public int NodeId { get; set; }

    // Primary time model (use sequence for ordering)
    public long SequenceFrom { get; set; }
    public long SequenceTo { get; set; }

    // Optional: actual time (for display / durations)
    public long StartUs { get; set; }
    public long EndUs { get; set; }

    // Thread that produced this span
    public int ThreadId { get; set; }

    // Attached activity (these will grow over time)
    public List<PageAccess> PageAccesses { get; } = new();
    public List<WaitSpan> Waits { get; } = new();
    public List<LockSpan> Locks { get; } = new();

    // Optional metrics (aggregated)
    public int LogicalReads { get; set; }
    public int Writes { get; set; }
    public long TotalWaitUs { get; set; }

    public long DurationUs => EndUs - StartUs;
}