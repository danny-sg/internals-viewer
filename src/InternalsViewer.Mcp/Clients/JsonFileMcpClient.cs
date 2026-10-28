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

    public bool IsInstalled => InstallDirectories.Any(Directory.Exists);

    protected abstract string[] InstallDirectories { get; }

    protected abstract string ServersKey { get; }

    protected virtual bool CanCreateConfig => false;

    protected virtual string MissingConfigMessage => $"Open the MCP configuration in {Name} first, then Register";

    public virtual Task<McpClientState> GetStateAsync(McpHostSettings settings, CancellationToken cancellationToken)
    {
        if (!IsInstalled)
        {
            return Task.FromResult(new McpClientState(McpClientStatus.NotInstalled, $"{Name} is not installed"));
        }

        var state = FindConfigPath() is { } path
            ? McpServerConfigFile.GetState(path, ServersKey, settings, IsCurrent)
            : new McpClientState(McpClientStatus.NotRegistered, MissingConfigMessage);

        return Task.FromResult(state);
    }

    public Task RegisterAsync(McpHostSettings settings, CancellationToken cancellationToken)
    {
        var path = FindConfigPath() ?? throw new McpClientException(MissingConfigMessage);

        McpServerConfigFile.Update(path, ServersKey, servers => servers[McpHostSettings.ServerName] = CreateEntry(settings));

        return Task.CompletedTask;
    }

    public Task UnregisterAsync(CancellationToken cancellationToken)
    {
        if (FindConfigPath() is { } path && File.Exists(path))
        {
            McpServerConfigFile.Update(path, ServersKey, servers => servers.Remove(McpHostSettings.ServerName));
        }

        return Task.CompletedTask;
    }

    protected abstract string[] GetConfigPaths();

    protected abstract JsonObject CreateEntry(McpHostSettings settings);

    protected virtual bool IsCurrent(JsonObject entry, McpHostSettings settings) => McpServerConfigFile.IsCurrent(entry, settings);

    private string? FindConfigPath()
    {
        var paths = GetConfigPaths();

        return paths.FirstOrDefault(File.Exists) ?? (CanCreateConfig ? paths[0] : null);
    }
}
