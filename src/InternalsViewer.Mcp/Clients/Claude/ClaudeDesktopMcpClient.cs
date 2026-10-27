using System.Text.Json.Nodes;
using InternalsViewer.Mcp.Clients.Enums;
using InternalsViewer.Mcp.Hosting;

namespace InternalsViewer.Mcp.Clients.Claude;

public sealed class ClaudeDesktopMcpClient(McpClientEnvironment environment) : JsonFileMcpClient
{
    public const string TokenVariable = "INTERNALS_VIEWER_TOKEN";

    public const string SetupUrl = "https://danny-sg.github.io/internals-viewer/docs/user-guide/mcp-server#claude-desktop";

    public override string Id => "claude-desktop";

    public override string Name => "Claude Desktop";

    public override string Description
        => "Adds a bridge to claude_desktop_config.json that Claude Desktop starts with Node.js, which you install yourself";

    public override string? HelpUrl => SetupUrl;

    protected override string InstallDirectory { get; } = environment.GetApplicationDataPath("Claude");

    protected override string ConfigPath { get; } = environment.GetApplicationDataPath("Claude", "claude_desktop_config.json");

    protected override string ServersKey => "mcpServers";

    private McpClientEnvironment Environment { get; } = environment;

    public override Task<McpClientState> GetStateAsync(McpHostSettings settings, CancellationToken cancellationToken)
    {
        if (IsInstalled && Environment.FindExecutable("npx") is null)
        {
            return Task.FromResult(new McpClientState(McpClientStatus.NotInstalled, "Node.js is not installed, see the setup instructions"));
        }

        return base.GetStateAsync(settings, cancellationToken);
    }

    protected override JsonObject CreateEntry(McpHostSettings settings) => new()
    {
        ["command"] = "cmd",
        ["args"] = new JsonArray("/c", "npx", "-y", "mcp-remote", settings.Endpoint, "--header", AuthorizationArgument),
        ["env"] = new JsonObject { [TokenVariable] = settings.Token }
    };

    private static string AuthorizationArgument => $"Authorization: Bearer ${{{TokenVariable}}}";

    protected override bool IsCurrent(JsonObject entry, McpHostSettings settings)
    {
        var token = entry["env"]?[TokenVariable]?.GetValue<string>();

        var hasEndpoint = entry["args"] is JsonArray arguments && arguments.Any(a => a?.GetValue<string>() == settings.Endpoint);

        return hasEndpoint && token == settings.Token;
    }
}
