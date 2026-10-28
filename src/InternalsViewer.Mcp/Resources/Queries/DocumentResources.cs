using System.ComponentModel;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Models.Context.Queries;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Resources.Queries;

[McpServerResourceType]
public sealed class DocumentResources(McpDocumentResolver documents)
{
    private McpDocumentResolver Documents { get; } = documents;

    [McpServerResource(UriTemplate = "internals-viewer://documents/{document}/query.sql", Name = "query", MimeType = "text/plain")]
    [Description("The SQL text of a query document")]
    public async Task<string> Query(string document, CancellationToken cancellationToken)
    {
        var context = await Documents.GetContextAsync<QueryContext>(document, cancellationToken);

        return context.Run?.SqlText ?? context.Sql;
    }

    [McpServerResource(UriTemplate = "internals-viewer://documents/{document}/plans/{index}.xml",
                       Name = "plan",
                       MimeType = "application/xml")]
    [Description("The actual execution plan XML (showplan) of a query document's run")]
    public async Task<string> Plan(string document, int index, CancellationToken cancellationToken)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var plans = scope.Result.ExecutionPlans;

        if (index < 0 || index >= plans.Count)
        {
            throw new McpException($"Document {scope.Query.TabId} has {plans.Count} plans.");
        }

        return plans[index].Xml ?? string.Empty;
    }
}
