namespace InternalsViewer.Query;

public sealed record EventOptions
{
    public bool IncludeLock { get; set; } = true;

    public bool IncludeWait { get; set; } = true;

    public bool IncludeMemory { get; set; }

    public bool IncludeCallStack { get; set; }

    public bool IncludeLatch { get; set; } = true;

    public bool IncludeSystemObjects { get; set; } 

    /// <summary>
    /// Trim events (and the call stack) outside the executed query's time window, dropping surrounding noise
    /// </summary>
    public bool CropToQuery { get; set; } = true;
}