using InternalsViewer.Mcp.Clients;
using InternalsViewer.Mcp.Hosting;

namespace InternalsViewer.Mcp.Interfaces.Clients;

public interface IMcpClient
{
    string Id { get; }

    string Name { get; }

    string Description { get; }

    string? HelpUrl { get; }

    bool IsInstalled { get; }

    Task<McpClientState> GetStateAsync(McpHostSettings settings, CancellationToken cancellationToken);

    Task RegisterAsync(McpHostSettings settings, CancellationToken cancellationToken);

    Task UnregisterAsync(CancellationToken cancellationToken);
}
