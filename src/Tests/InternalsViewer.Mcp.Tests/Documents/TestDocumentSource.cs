using InternalsViewer.Mcp.Interfaces.Tabs;

namespace InternalsViewer.Mcp.Tests.Documents;

internal sealed class TestDocumentSource(params IMcpTab[] documents) : IMcpTabSource
{
    public List<IMcpTab> Documents { get; } = [.. documents];

    public Task<List<IMcpTab>> GetTabsAsync(CancellationToken cancellationToken) => Task.FromResult(new List<IMcpTab>(Documents));
}
