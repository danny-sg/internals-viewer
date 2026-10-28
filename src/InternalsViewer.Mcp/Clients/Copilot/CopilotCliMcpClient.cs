using InternalsViewer.Mcp.Hosting;
using InternalsViewer.Mcp.Interfaces.Clients;

namespace InternalsViewer.Mcp.Clients.Copilot;

public sealed class CopilotCliMcpClient(McpClientEnvironment environment, ICommandRunner runner) : CommandLineMcpClient(environment, runner)
{
    public override string Id => "copilot-cli";

    public override string Name => "GitHub Copilot CLI";

    public override string Description => "Registers the server with the copilot command line in its user configuration";

    protected override string ExecutableName => "copilot";

    protected override string ConfigPath { get; } = environment.GetHomePath(".copilot", "mcp-config.json");

    protected override string ServersKey => "mcpServers";

    protected override string[] GetAddArguments(McpHostSettings settings) =>
    [
        "mcp", "add", "--transport", "http", McpHostSettings.ServerName, settings.Endpoint,
        "--header", $"Authorization: {settings.AuthorizationHeader}"
    ];

    protected override string[] GetRemoveArguments() => ["mcp", "remove", McpHostSettings.ServerName];
}
