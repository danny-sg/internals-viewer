using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Query.Export;

namespace InternalsViewer.Mcp.Models.Context.Queries;

public sealed record QueryContext : TabContext, IAreaContext
{
    public static McpArea Area => McpArea.Query;

    public string Sql { get; init; } = string.Empty;

    public QueryExportContent? Run { get; init; }

    public McpSelection Selection { get; init; } = new();
}
