using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Query.FullTrace;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools;

[McpServerToolType]
public sealed class TabTools(McpDocumentResolver documents)
{
    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "list_tabs", ReadOnly = true, Idempotent = true)]
    [Description("The tabs open in Internals Viewer: their ids, area (Query, Allocations, Page, Index or Columnstore), database " +
                 "and the active one. A Query tab also shows whether a run and a Full Trace are loaded. Internals Viewer is a " +
                 "learning tool for how SQL Server executes a query, so explain the mechanism rather than tune the query. " +
                 "Call this first, then query_summary")]
    public async Task<string> ListTabs(CancellationToken cancellationToken)
    {
        var all = await Documents.GetTabsAsync(cancellationToken);

        if (all.Count == 0)
        {
            return "No tabs are open. Connect to a database in Internals Viewer first.";
        }

        var table = new TextTable("Id", "Active", "Area", "Title", "Server", "Database", "Detail");

        foreach (var tab in all)
        {
            table.Add(tab.Id,
                      tab.IsActive ? "yes" : string.Empty,
                      tab.Area.ToString(),
                      tab.Title,
                      tab.ServerName,
                      tab.DatabaseName,
                      await DocumentDetailAsync(tab, cancellationToken));
        }

        var builder = new StringBuilder();

        table.Write(builder);

        builder.AppendLine();

        builder.AppendLine("Pass a tab id to the other tools, or leave it out for the active tab of that area.");

        return builder.ToString();
    }

    [McpServerTool(Name = "show_tab")]
    [Description("Switch Internals Viewer to a tab so the user sees it. The ids come from list_tabs")]
    public async Task<string> ShowTab([Description("Tab id from list_tabs")] string tab, CancellationToken cancellationToken)
    {
        var target = await Documents.ResolveTabAsync(tab, cancellationToken);

        await RunAsync(() => target.ActivateAsync(cancellationToken));

        return $"Switched to {target.Id} ({target.Area}: {target.Title}).";
    }

    [McpServerTool(Name = "close_tab", Destructive = true)]
    [Description("Close a tab in Internals Viewer. Anything unsaved in the tab, such as a query run that has not been exported, " +
                 "is lost. The ids come from list_tabs")]
    public async Task<string> CloseTab([Description("Tab id from list_tabs")] string tab, CancellationToken cancellationToken)
    {
        var target = await Documents.ResolveTabAsync(tab, cancellationToken);

        await RunAsync(() => target.CloseAsync(cancellationToken));

        return $"Closed {target.Id} ({target.Area}: {target.Title}).";
    }

    private static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
        {
            throw new McpException(exception.Message);
        }
    }

    private static async Task<string> DocumentDetailAsync(IMcpTab tab, CancellationToken cancellationToken)
    {
        if (tab.Area != McpArea.Query)
        {
            return string.Empty;
        }

        var context = (QueryContext)await tab.GetContextAsync(cancellationToken);

        var run = context.Run;

        var sql = run?.SqlText ?? context.Sql;

        var firstLine = sql.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;

        var state = run is null ? "no run" : run.Result.IsSuccess ? "run" : "run failed";

        var trace = run?.ExtraOf<TraceResult>() is null ? string.Empty : ", Full Trace";

        return $"{state}{trace}: {Format.Truncate(firstLine, 60)}";
    }
}
