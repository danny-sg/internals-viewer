using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Query.Export;

namespace InternalsViewer.Mcp.Interfaces.Documents;

public interface IMcpDocument : IMcpTab
{
    McpArea IMcpTab.Area => McpArea.Query;

    string SqlText { get; }

    QueryExportContent? Run { get; }

    IMcpNavigator? Navigator { get; }

    async Task<TabContext> IMcpTab.GetContextAsync(CancellationToken cancellationToken)
        => new QueryContext
        {
            TabId = Id,
            Title = Title,
            Database = Database,
            Sql = SqlText,
            Run = Run,
            Selection = Navigator is { } navigator ? await navigator.GetSelectionAsync(cancellationToken) : new McpSelection()
        };
}