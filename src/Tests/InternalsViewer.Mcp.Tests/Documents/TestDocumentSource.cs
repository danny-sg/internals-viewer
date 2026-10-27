using InternalsViewer.Mcp.Interfaces.Documents;

namespace InternalsViewer.Mcp.Tests.Documents;

internal sealed class TestDocumentSource(params IMcpDocument[] documents) : IMcpDocumentSource
{
    public List<IMcpDocument> Documents { get; } = [.. documents];

    public Task<List<IMcpDocument>> GetDocumentsAsync(CancellationToken cancellationToken) => Task.FromResult(Documents.ToList());
}
