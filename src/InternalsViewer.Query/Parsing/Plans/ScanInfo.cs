namespace InternalsViewer.Query.Parsing.Plans;

public sealed class ScanInfo
{
    public bool? IsForward { get; set; } = true;

    public bool? IsOutputOrdered { get; set; }

    public bool IsLookup { get; set; }
}