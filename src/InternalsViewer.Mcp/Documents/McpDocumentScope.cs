using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Documents;

public sealed class McpDocumentScope(
    IMcpTab tab,
    QueryContext query,
    QueryExportContent run,
    DocumentContext context)
{
    public QueryContext Query { get; } = query;

    public QueryExportContent Run { get; } = run;

    public DocumentContext Context { get; } = context;

    public McpSelection Selection => Query.Selection;

    public QueryResult Result => Run.Result;

    public TraceResult? Trace => Context.Trace;

    public long CropStartUs => Result.CropStartUs ?? 0;

    private IMcpTab Tab { get; } = tab;

    public TraceResult RequireTrace()
        => Trace
           ?? throw new McpException($"Document {Query.TabId} has no Full Trace. Only the events, plan and call stacks are available.");

    public async Task RunAsync(Func<IQueryCommands, Task> command, CancellationToken cancellationToken)
    {
        try
        {
            await Tab.RunAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            throw new McpException(exception.Message);
        }
    }
}
