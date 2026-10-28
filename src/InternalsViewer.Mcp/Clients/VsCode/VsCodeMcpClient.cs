using System.Text.Json.Nodes;
using InternalsViewer.Mcp.Hosting;

namespace InternalsViewer.Mcp.Clients.VsCode;

public sealed class VsCodeMcpClient(McpClientEnvironment environment) : JsonFileMcpClient
{
    public override string Id => "vscode";

    public override string Name => "Visual Studio Code";

    public override string Description
        => "Adds the server to the user mcp.json of Visual Studio Code, for Copilot agent mode in every workspace";

    protected override string[] InstallDirectories { get; } = [environment.GetApplicationDataPath("Code", "User")];

    protected override string ServersKey => "servers";

    protected override string MissingConfigMessage => "Run MCP: Open User Configuration in Visual Studio Code first, then Register";

    private string ConfigPath { get; } = environment.GetApplicationDataPath("Code", "User", "mcp.json");

    protected override string[] GetConfigPaths() => [ConfigPath];

    protected override JsonObject CreateEntry(McpHostSettings settings) => McpServerConfigFile.CreateEntry(settings, "http");
}
