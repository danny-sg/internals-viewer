namespace InternalsViewer.Query.Parsing.Plans;

public sealed record CorrelatedSeekColumn(string Column, string OuterTable, string OuterColumn)
{
    public string OuterReference => $"{OuterTable}.{OuterColumn}";
}
