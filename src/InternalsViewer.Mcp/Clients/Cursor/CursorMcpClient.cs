using System.Text.Json.Nodes;
using InternalsViewer.Mcp.Hosting;

namespace InternalsViewer.Mcp.Clients.Cursor;

public sealed class CursorMcpClient(McpClientEnvironment environment) : JsonFileMcpClient
{
    public override string Id => "cursor";

    public override string Name => "Cursor";

    public override string Description => "Adds the server to Cursor's global mcp.json, so it is available in every project";

    protected override string[] InstallDirectories { get; } = [environment.GetHomePath(".cursor")];

    protected override string ServersKey => "mcpServers";

    protected override bool CanCreateConfig => true;

    private string ConfigPath { get; } = environment.GetHomePath(".cursor", "mcp.json");

    protected override string[] GetConfigPaths() => [ConfigPath];

    protected override JsonObject CreateEntry(McpHostSettings settings) => McpServerConfigFile.CreateEntry(settings);
}
