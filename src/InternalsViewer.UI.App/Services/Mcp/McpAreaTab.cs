using System;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.UI.Dispatching;

namespace InternalsViewer.UI.App.Services.Mcp;

internal sealed class McpAreaTab(string id,
                                 McpArea area,
                                 bool isActive,
                                 TabViewModel viewModel,
                                 DatabaseSource? database,
                                 DispatcherQueue dispatcher) : IMcpTab
{
    public string Id { get; } = id;

    public string Title { get; } = viewModel.Name;

    public McpArea Area { get; } = area;

    public bool IsActive { get; } = isActive;

    public string ServerName { get; } = database?.Connection.Name ?? string.Empty;

    public string DatabaseName { get; } = database?.Name ?? string.Empty;

    public DatabaseSource? Database { get; } = database;

    private TabViewModel ViewModel { get; } = viewModel;

    private DispatcherQueue Dispatcher { get; } = dispatcher;

    private MainWindow Window => App.MainWindow ?? throw new InvalidOperationException("The Internals Viewer window is not available.");

    public Task<TabContext> GetContextAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel is not IMcpContextSource source)
        {
            throw new NotSupportedException($"The {Area} tab {Id} provides no context.");
        }

        return RunOnUiThreadAsync(() => Task.FromResult(source.GetContext() with { TabId = Id }), cancellationToken);
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => RunOnUiThreadAsync(() =>
        {
            Window.ActivateTab(ViewModel);

            return Task.FromResult(true);
        }, cancellationToken);

    public Task CloseAsync(CancellationToken cancellationToken = default)
        => RunOnUiThreadAsync(() =>
        {
            Window.CloseTab(ViewModel);

            return Task.FromResult(true);
        }, cancellationToken);

    public Task<IMcpTab> GetPageAsync(PageAddress? address, CancellationToken cancellationToken = default)
        => RunOnUiThreadAsync(() =>
        {
            if (ViewModel is not QueryViewModel query)
            {
                throw new NotSupportedException($"The {Area} tab {Id} has no page dock.");
            }

            var (page, isActive) = query.GetOpenPage(address);

            IMcpTab tab = new McpAreaTab($"{Id}/{page.PageAddress}", McpArea.Page, isActive, page, Database, Dispatcher);

            return Task.FromResult(tab);
        }, cancellationToken);

    public Task RunAsync<TCommands>(Func<TCommands, Task> command, CancellationToken cancellationToken = default)
        where TCommands : class
        => RunAsync<TCommands, bool>(async c =>
        {
            await command(c);

            return true;
        }, cancellationToken);

    public Task<TResult> RunAsync<TCommands, TResult>(
        Func<TCommands, Task<TResult>> command,
        CancellationToken cancellationToken = default)
        where TCommands : class
    {
        if (ViewModel is not TCommands commands)
        {
            throw new NotSupportedException($"The {Area} tab {Id} does not accept {typeof(TCommands).Name} commands.");
        }

        return RunOnUiThreadAsync(() => command(commands), cancellationToken);
    }

    private Task<TResult> RunOnUiThreadAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        var queued = Dispatcher.TryEnqueue(async () =>
        {
            try
            {
                completion.SetResult(await action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        if (!queued)
        {
            throw new InvalidOperationException("The Internals Viewer window is not available.");
        }

        return completion.Task.WaitAsync(cancellationToken);
    }
}