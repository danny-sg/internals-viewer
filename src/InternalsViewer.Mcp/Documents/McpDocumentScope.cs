using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Documents;

public sealed class McpDocumentScope(
    QueryContext query,
    QueryExportContent run,
    DocumentContext context,
    IMcpNavigator? navigator)
{
    public QueryContext Query { get; } = query;

    public QueryExportContent Run { get; } = run;

    public DocumentContext Context { get; } = context;

    public McpSelection Selection => Query.Selection;

    public QueryResult Result => Run.Result;

    public TraceResult? Trace => Context.Trace;

    public long CropStartUs => Result.CropStartUs ?? 0;

    private IMcpNavigator? Navigator { get; } = navigator;

    public TraceResult RequireTrace()
        => Trace
           ?? throw new McpException($"Document {Query.TabId} has no Full Trace. Only the events, plan and call stacks are available.");

    public IMcpNavigator RequireNavigator()
        => Navigator ?? throw new McpException($"Document {Query.TabId} cannot be navigated from here.");
}