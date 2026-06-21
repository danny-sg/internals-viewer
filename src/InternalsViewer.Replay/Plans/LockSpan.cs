namespace InternalsViewer.Replay.Plans;

public class LockSpan
{
    public string Mode { get; set; }

    public long StartUs { get; set; }

    public long EndUs { get; set; }

    public long DurationUs => EndUs - StartUs;
}