using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Mcp.Interfaces.Tabs;

namespace InternalsViewer.Mcp.Tests.Documents;

internal sealed class TestDocumentSource(params IMcpDocument[] documents) : IMcpTabSource
{
    public List<IMcpDocument> Documents { get; } = [.. documents];

    public Task<List<IMcpTab>> GetTabsAsync(CancellationToken cancellationToken) => Task.FromResult(Documents.Cast<IMcpTab>().ToList());
}
