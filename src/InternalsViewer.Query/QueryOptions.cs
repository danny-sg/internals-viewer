namespace InternalsViewer.Query;

public sealed record QueryOptions
{
    public bool ClearBufferPool { get; set; } = true;

    public bool DisableReadAhead { get; set; } = true;

    public bool IncludeResults { get; set; } = true;

    public bool Trace { get; set; } = true;
}