namespace InternalsViewer.Mcp.Interfaces.Documents;

public interface IMcpDocumentSource
{
    Task<List<IMcpDocument>> GetDocumentsAsync(CancellationToken cancellationToken);
}
