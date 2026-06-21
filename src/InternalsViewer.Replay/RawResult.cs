namespace InternalsViewer.Replay;

public sealed record RawResult
{
    public required string QueryPlan { get; set; }

    public required string Events { get; set; }
}