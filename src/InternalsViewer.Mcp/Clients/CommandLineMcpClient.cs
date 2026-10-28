using InternalsViewer.Mcp.Clients.Enums;
using InternalsViewer.Mcp.Hosting;
using InternalsViewer.Mcp.Interfaces.Clients;

namespace InternalsViewer.Mcp.Clients;

public abstract class CommandLineMcpClient(McpClientEnvironment environment, ICommandRunner runner) : IMcpClient
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public string? HelpUrl => null;

    public bool IsInstalled => Executable is not null;

    protected abstract string ExecutableName { get; }

    protected abstract string ConfigPath { get; }

    protected abstract string ServersKey { get; }

    private McpClientEnvironment Environment { get; } = environment;

    private ICommandRunner Runner { get; } = runner;

    private string? Executable => Environment.FindExecutable(ExecutableName);

    public Task<McpClientState> GetStateAsync(McpHostSettings settings, CancellationToken cancellationToken)
    {
        var state = IsInstalled
            ? McpServerConfigFile.GetState(ConfigPath, ServersKey, settings)
            : new McpClientState(McpClientStatus.NotInstalled, $"{Name} is not installed");

        return Task.FromResult(state);
    }

    public async Task RegisterAsync(McpHostSettings settings, CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(settings, cancellationToken);

        if (state.IsRegistered)
        {
            await RunAsync(GetRemoveArguments(), cancellationToken, ignoreFailure: true);
        }

        await RunAsync(GetAddArguments(settings), cancellationToken);
    }

    public Task UnregisterAsync(CancellationToken cancellationToken) => RunAsync(GetRemoveArguments(), cancellationToken);

    protected abstract string[] GetAddArguments(McpHostSettings settings);

    protected abstract string[] GetRemoveArguments();

    private async Task RunAsync(string[] arguments, CancellationToken cancellationToken, bool ignoreFailure = false)
    {
        var executable = Executable ?? throw new McpClientException($"{Name} is not installed.");

        var result = await Runner.RunAsync(executable, arguments, cancellationToken);

        if (result.ExitCode != 0 && !ignoreFailure)
        {
            throw new McpClientException($"{Name} returned exit code {result.ExitCode}: {result.Output}");
        }
    }
}
