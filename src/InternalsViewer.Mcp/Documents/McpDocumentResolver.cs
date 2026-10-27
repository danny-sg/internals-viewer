using System.Runtime.CompilerServices;
using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Query.Export;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Documents;

public sealed class McpDocumentResolver(IMcpDocumentSource documents)
{
    private IMcpDocumentSource Documents { get; } = documents;

    private ConditionalWeakTable<QueryExportContent, DocumentContext> Contexts { get; } = [];

    public Task<List<IMcpDocument>> GetDocumentsAsync(CancellationToken cancellationToken) => Documents.GetDocumentsAsync(cancellationToken);

    public async Task<IMcpDocument> ResolveDocumentAsync(string? documentId, CancellationToken cancellationToken)
    {
        var all = await Documents.GetDocumentsAsync(cancellationToken);

        if (all.Count == 0)
        {
            throw new McpException("No query documents are open in Internals Viewer. Open a database and a Query tab first.");
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

        var run = document.Run
                  ?? throw new McpException($"Document {document.Id} has no query run yet. Run the query in Internals Viewer first.");

        return new McpDocumentScope(document, run, Contexts.GetValue(run, r => new DocumentContext(r)));
    }

    public DocumentContext GetContext(QueryExportContent run) => Contexts.GetValue(run, r => new DocumentContext(r));
}
