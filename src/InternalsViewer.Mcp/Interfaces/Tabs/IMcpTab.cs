using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Tabs;

namespace InternalsViewer.Mcp.Interfaces.Tabs;

public interface IMcpTab
{
    string Id { get; }

    string Title { get; }

    McpArea Area { get; }

    bool IsActive { get; }

    string ServerName { get; }

    string DatabaseName { get; }

    DatabaseSource? Database { get; }

    Task<TabContext> GetContextAsync(CancellationToken cancellationToken = default);

    Task ActivateAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Tab {Id} ({Area}) cannot be switched to.");

    Task CloseAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Tab {Id} ({Area}) cannot be closed.");

    Task<string> RunAsync<TCommands>(Func<TCommands, Task<string>> command, CancellationToken cancellationToken = default)
        where TCommands : class
        => throw new NotSupportedException($"Tab {Id} ({Area}) does not accept {typeof(TCommands).Name} commands.");

    async Task RunAsync<TCommands>(Func<TCommands, Task> command, CancellationToken cancellationToken = default)
        where TCommands : class
        => await RunAsync<TCommands>(async c =>
        {
            await command(c);

            return string.Empty;
        }, cancellationToken);
}