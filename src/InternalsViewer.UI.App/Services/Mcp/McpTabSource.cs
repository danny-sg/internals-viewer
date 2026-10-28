using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using InternalsViewer.UI.App.ViewModels.Database;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.ViewModels.Page;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.ViewModels.Tabs;

namespace InternalsViewer.UI.App.Services.Mcp;

public sealed class McpTabSource : IMcpTabSource
{
    private ConditionalWeakTable<TabViewModel, string> Ids { get; } = [];

    private Dictionary<McpArea, int> Counts { get; } = [];

    public Task<List<IMcpTab>> GetTabsAsync(CancellationToken cancellationToken)
    {
        if (App.MainWindow is not { } window)
        {
            return Task.FromResult(new List<IMcpTab>());
        }

        var completion = new TaskCompletionSource<List<IMcpTab>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var queued = window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                completion.SetResult(Snapshot(window));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        return queued ? completion.Task.WaitAsync(cancellationToken) : Task.FromResult(new List<IMcpTab>());
    }

    private static string GetPrefix(McpArea area)
        => area switch
        {
            McpArea.Query => "q",
            McpArea.Allocations => "a",
            McpArea.Page => "p",
            McpArea.Index => "i",
            _ => "c"
        };

    private static DatabaseSource? GetDatabase(TabViewModel viewModel)
        => viewModel switch
        {
            QueryViewModel query => query.Database,
            DatabaseTabViewModel database => database.Database,
            PageTabViewModel page => page.Database,
            IndexTabViewModel index => index.Database,
            ColumnstoreTabViewModel columnstore => columnstore.Database,
            _ => null
        };

    private List<IMcpTab> Snapshot(MainWindow window)
    {
        var tabs = new List<IMcpTab>();

        foreach (var (viewModel, area, isActive) in window.GetTabs())
        {
            var id = GetId(viewModel, area);

            var database = GetDatabase(viewModel);

            tabs.Add(new McpAreaTab(id, area, isActive, viewModel, database, window.DispatcherQueue));
        }

        return tabs;
    }

    private string GetId(TabViewModel viewModel, McpArea area)
        => Ids.GetValue(viewModel, _ =>
        {
            var count = Counts.GetValueOrDefault(area) + 1;

            Counts[area] = count;

            return $"{GetPrefix(area)}{count}";
        });
}
