using InternalsViewer.Mcp.Hosting;
using InternalsViewer.Mcp.Interfaces.Clients;

namespace InternalsViewer.Mcp.Clients.Claude;

public sealed class ClaudeCodeMcpClient(McpClientEnvironment environment, ICommandRunner runner) : CommandLineMcpClient(environment, runner)
{
    public override string Id => "claude-code";

    public override string Name => "Claude Code";

    public override string Description => "Registers the server with the claude command line at user scope, for every project";

    protected override string ExecutableName => "claude";

    protected override string ConfigPath { get; } = environment.GetHomePath(".claude.json");

    protected override string ServersKey => "mcpServers";

    protected override string[] GetAddArguments(McpHostSettings settings) =>
    [
        "mcp", "add", "--transport", "http", "-s", "user", McpHostSettings.ServerName, settings.Endpoint,
        "--header", $"Authorization: {settings.AuthorizationHeader}"
    ];

    protected override string[] GetRemoveArguments() => ["mcp", "remove", "-s", "user", McpHostSettings.ServerName];
}
