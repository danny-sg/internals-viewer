using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Documents;

public sealed class McpDocumentScope(IMcpDocument document, QueryExportContent run, DocumentContext context)
{
    public IMcpDocument Document { get; } = document;

    public QueryExportContent Run { get; } = run;

    public DocumentContext Context { get; } = context;

    public QueryResult Result => Run.Result;

    public TraceResult? Trace => Context.Trace;

    public long CropStartUs => Result.CropStartUs ?? 0;

    public TraceResult RequireTrace()
        => Trace
           ?? throw new McpException($"Document {Document.Id} has no Full Trace. Only the events, plan and call stacks are available.");

    public IMcpNavigator RequireNavigator()
        => Document.Navigator ?? throw new McpException($"Document {Document.Id} cannot be navigated from here.");
}
