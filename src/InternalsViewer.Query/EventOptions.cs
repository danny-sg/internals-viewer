namespace InternalsViewer.Query;

public sealed record EventOptions
{
    public bool IncludeLock { get; set; } = true;

    public bool IncludeWait { get; set; } = true;

    public bool IncludeCallstack { get; set; }
}