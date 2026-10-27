using InternalsViewer.Mcp.Clients.Enums;

namespace InternalsViewer.Mcp.Clients;

public sealed record McpClientState(McpClientStatus Status, string Detail)
{
    public bool IsRegistered => Status is McpClientStatus.Registered or McpClientStatus.NeedsUpdate;
}
