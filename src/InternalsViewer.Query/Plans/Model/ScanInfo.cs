namespace InternalsViewer.Query.Plans.Model;

public sealed class ScanInfo
{
    public bool? IsForward { get; set; } = true;

    public bool? IsOutputOrdered { get; set; }

    public bool IsLookup { get; set; }

    public bool IsForcedIndex { get; set; }

    public bool IsForceSeek { get; set; }

    public bool IsForceScan { get; set; }
}