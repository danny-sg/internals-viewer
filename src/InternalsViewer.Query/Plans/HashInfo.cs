namespace InternalsViewer.Query.Plans;

public sealed class HashInfo
{
    public List<ColumnRef> BuildKeys { get; set; } = [];

    public List<ColumnRef> ProbeKeys { get; set; } = [];
}

public sealed class ScanInfo
{
    public bool? IsForward { get; set; } = true;

    public bool? IsOutputOrdered { get; set; }
}