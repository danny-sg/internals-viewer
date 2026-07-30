namespace InternalsViewer.Query.Plans.Model;

public sealed class HashInfo
{
    public List<ColumnReference> BuildKeys { get; set; } = [];

    public List<ColumnReference> ProbeKeys { get; set; } = [];
}