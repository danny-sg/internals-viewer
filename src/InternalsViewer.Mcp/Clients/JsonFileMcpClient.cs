using System.Text.Json.Nodes;
using InternalsViewer.Mcp.Clients.Enums;
using InternalsViewer.Mcp.Hosting;
using InternalsViewer.Mcp.Interfaces.Clients;

namespace InternalsViewer.Mcp.Clients;

public abstract class JsonFileMcpClient : IMcpClient
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public virtual string? HelpUrl => null;

    public bool IsInstalled => Directory.Exists(InstallDirectory);

    protected abstract string InstallDirectory { get; }

    protected abstract string ConfigPath { get; }

    protected abstract string ServersKey { get; }

    public virtual Task<McpClientState> GetStateAsync(McpHostSettings settings, CancellationToken cancellationToken)
    {
        var state = IsInstalled
            ? McpServerConfigFile.GetState(ConfigPath, ServersKey, settings, IsCurrent)
            : new McpClientState(McpClientStatus.NotInstalled, $"{Name} is not installed");

        return Task.FromResult(state);
    }

    public Task RegisterAsync(McpHostSettings settings, CancellationToken cancellationToken)
    {
        McpServerConfigFile.Update(ConfigPath, ServersKey, servers => servers[McpHostSettings.ServerName] = CreateEntry(settings));

        return Task.CompletedTask;
    }

    public Task UnregisterAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(ConfigPath))
        {
            McpServerConfigFile.Update(ConfigPath, ServersKey, servers => servers.Remove(McpHostSettings.ServerName));
        }

        return Task.CompletedTask;
    }

    protected abstract JsonObject CreateEntry(McpHostSettings settings);

    protected virtual bool IsCurrent(JsonObject entry, McpHostSettings settings) => McpServerConfigFile.IsCurrent(entry, settings);
}
