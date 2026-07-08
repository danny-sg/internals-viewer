namespace InternalsViewer.Query;

public sealed record EventOptions
{
    public bool IncludeLock { get; set; } = true;

    public bool IncludeWait { get; set; } = true;

    public bool IncludeMemory { get; set; }

    public bool IncludeCallStack { get; set; }
}