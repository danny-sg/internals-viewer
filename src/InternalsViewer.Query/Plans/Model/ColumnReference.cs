namespace InternalsViewer.Query.Parsing.Plans;

public sealed record ColumnReference
{
    public string Database { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;
    
    public string Table { get; set; } = string.Empty;
    
    public string Column { get; set; } = string.Empty;

    public string TableKey =>
        $"{Schema}.{Table}".ToLowerInvariant();
}