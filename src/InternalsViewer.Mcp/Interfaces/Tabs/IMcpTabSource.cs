namespace InternalsViewer.Mcp.Interfaces.Tabs;

public interface IMcpTabSource
{
    Task<List<IMcpTab>> GetTabsAsync(CancellationToken cancellationToken);
}