using System.Runtime.CompilerServices;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Pages;
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

    public async Task<McpAreaSession<TContext>> OpenAsync<TContext>(string? tabId, CancellationToken cancellationToken)
        where TContext : TabContext, IAreaContext
    {
        var tab = await ResolveAreaTabAsync(TContext.Area, tabId, cancellationToken);

        var context = await tab.GetContextAsync(cancellationToken) as TContext
                      ?? throw new McpException($"Tab {tab.Id} has no {TContext.Area} context.");

        return new McpAreaSession<TContext>(tab, context);
    }

    public async Task<McpAreaSession<PageContext>> OpenQueryPageAsync(string? tabId,
                                                                       PageAddress? address,
                                                                       CancellationToken cancellationToken)
    {
        var tab = await ResolveAreaTabAsync(McpArea.Query, tabId, cancellationToken);

        IMcpTab page;

        try
        {
            page = await tab.GetPageAsync(address, cancellationToken);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
        {
            throw new McpException($"{exception.Message} Use query_page_goto to open one in Query tab {tab.Id}.");
        }

        var context = await page.GetContextAsync(cancellationToken) as PageContext
                      ?? throw new McpException($"Tab {page.Id} has no Page context.");

        return new McpAreaSession<PageContext>(page, context);
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

    public async Task<McpDocumentScope> ResolveAsync(string? documentId, CancellationToken cancellationToken)
    {
        var tab = await ResolveQueryTabAsync(documentId, cancellationToken);

        var query = await tab.GetContextAsync(cancellationToken) as QueryContext
                    ?? throw new McpException($"Tab {tab.Id} has no Query context.");

        return CreateScope(tab, query);
    }

    public DocumentContext GetContext(QueryExportContent run) => Contexts.GetValue(run, r => new DocumentContext(r));

    private async Task<IMcpTab> ResolveQueryTabAsync(string? documentId, CancellationToken cancellationToken)
    {
        if (documentId is not null)
        {
            return await ResolveAreaTabAsync(McpArea.Query, documentId, cancellationToken);
        }

        var queries = (await Tabs.GetTabsAsync(cancellationToken)).Where(t => t.Area == McpArea.Query).ToList();

        if (queries.Count == 0)
        {
            throw new McpException("No Query tabs are open in Internals Viewer. Open a database and a Query tab first.");
        }

        foreach (var tab in queries.OrderByDescending(t => t.IsActive))
        {
            if (await tab.GetContextAsync(cancellationToken) is QueryContext { Run: not null })
            {
                return tab;
            }
        }

        return queries.FirstOrDefault(t => t.IsActive) ?? queries[0];
    }

    private McpDocumentScope CreateScope(IMcpTab tab, QueryContext query)
    {
        var run = query.Run
                  ?? throw new McpException($"Document {tab.Id} has no query run yet. Run the query in Internals Viewer first.");

        return new McpDocumentScope(tab, query, run, Contexts.GetValue(run, r => new DocumentContext(r)));
    }
}
