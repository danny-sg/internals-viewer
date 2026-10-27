using System.Runtime.CompilerServices;
using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Query.Export;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Documents;

public sealed class McpDocumentResolver(IMcpTabSource tabs)
{
    private IMcpTabSource Tabs { get; } = tabs;

    private ConditionalWeakTable<QueryExportContent, DocumentContext> Contexts { get; } = [];

    public Task<List<IMcpTab>> GetTabsAsync(CancellationToken cancellationToken) => Tabs.GetTabsAsync(cancellationToken);

    public async Task<IMcpTab> ResolveDatabaseTabAsync(string? tabId, CancellationToken cancellationToken)
    {
        var all = await Tabs.GetTabsAsync(cancellationToken);

        var withDatabase = all.Where(t => t.Database is not null).ToList();

        if (tabId is not null)
        {
            var tab = all.FirstOrDefault(t => string.Equals(t.Id, tabId, StringComparison.OrdinalIgnoreCase))
                      ?? throw new McpException($"No tab {tabId}. Open tabs: {string.Join(", ", all.Select(t => t.Id))}.");

            return tab.Database is null ? throw new McpException($"Tab {tab.Id} has no database connection.") : tab;
        }

        return withDatabase.FirstOrDefault(t => t.IsActive)
               ?? withDatabase.FirstOrDefault()
               ?? throw new McpException("No database is open in Internals Viewer. Connect to a database first.");
    }

    public async Task<IMcpTab> ResolveTabAsync(string? tabId, CancellationToken cancellationToken)
    {
        var all = await Tabs.GetTabsAsync(cancellationToken);

        if (tabId is not null)
        {
            return all.FirstOrDefault(t => string.Equals(t.Id, tabId, StringComparison.OrdinalIgnoreCase))
                   ?? throw new McpException($"No tab {tabId}. Open tabs: {string.Join(", ", all.Select(t => t.Id))}.");
        }

        return all.FirstOrDefault(t => t.IsActive)
               ?? all.FirstOrDefault()
               ?? throw new McpException("No tabs are open in Internals Viewer.");
    }

    public async Task<TContext> GetContextAsync<TContext>(string? tabId, CancellationToken cancellationToken)
        where TContext : TabContext, IAreaContext
    {
        var tab = await ResolveAreaTabAsync(TContext.Area, tabId, cancellationToken);

        return await tab.GetContextAsync(cancellationToken) as TContext
               ?? throw new McpException($"Tab {tab.Id} has no {TContext.Area} context.");
    }

    public async Task<string> RunAsync<TCommands>(
        McpArea area,
        string? tabId,
        Func<TCommands, Task<string>> command,
        CancellationToken cancellationToken)
        where TCommands : class
    {
        var tab = await ResolveAreaTabAsync(area, tabId, cancellationToken);

        try
        {
            return await tab.RunAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            throw new McpException(exception.Message);
        }
    }

    public async Task<IMcpTab> ResolveAreaTabAsync(McpArea area, string? tabId, CancellationToken cancellationToken)
    {
        var all = await Tabs.GetTabsAsync(cancellationToken);

        var inArea = all.Where(t => t.Area == area).ToList();

        if (tabId is not null)
        {
            var tab = all.FirstOrDefault(t => string.Equals(t.Id, tabId, StringComparison.OrdinalIgnoreCase))
                      ?? throw new McpException($"No tab {tabId}. Open tabs: {string.Join(", ", all.Select(t => t.Id))}.");

            return tab.Area == area ? tab : throw new McpException($"Tab {tab.Id} is a {tab.Area} tab, not {area}.");
        }

        return inArea.FirstOrDefault(t => t.IsActive)
               ?? inArea.FirstOrDefault()
               ?? throw new McpException($"No {area} tab is open in Internals Viewer. Open one first.");
    }

    public async Task<List<IMcpDocument>> GetDocumentsAsync(CancellationToken cancellationToken)
        => [.. (await Tabs.GetTabsAsync(cancellationToken)).OfType<IMcpDocument>()];

    public async Task<IMcpDocument> ResolveDocumentAsync(string? documentId, CancellationToken cancellationToken)
    {
        var all = await GetDocumentsAsync(cancellationToken);

        if (all.Count == 0)
        {
            throw new McpException("No Query tabs are open in Internals Viewer. Open a database and a Query tab first.");
        }

        if (documentId is null)
        {
            return all.FirstOrDefault(d => d is { IsActive: true, Run: not null })
                   ?? all.FirstOrDefault(d => d.Run is not null)
                   ?? all.First(d => d.IsActive);
        }

        return all.FirstOrDefault(d => string.Equals(d.Id, documentId, StringComparison.OrdinalIgnoreCase))
               ?? throw new McpException($"No document {documentId}. Open documents: {string.Join(", ", all.Select(d => d.Id))}.");
    }

    public async Task<McpDocumentScope> ResolveAsync(string? documentId, CancellationToken cancellationToken)
    {
        var document = await ResolveDocumentAsync(documentId, cancellationToken);

        var query = await document.GetContextAsync(cancellationToken) as QueryContext
                    ?? throw new McpException($"Tab {document.Id} has no Query context.");

        var run = query.Run
                  ?? throw new McpException($"Document {document.Id} has no query run yet. Run the query in Internals Viewer first.");

        return new McpDocumentScope(query, run, Contexts.GetValue(run, r => new DocumentContext(r)), document.Navigator);
    }

    public DocumentContext GetContext(QueryExportContent run) => Contexts.GetValue(run, r => new DocumentContext(r));
}
