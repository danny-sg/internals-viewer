namespace InternalsViewer.Replay.Plans;

public class WaitSpan
{
    public string WaitType { get; set; }

    public long StartUs { get; set; }
    public long EndUs { get; set; }

    public long DurationUs => EndUs - StartUs;
}