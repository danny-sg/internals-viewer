using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Allocations;
using InternalsViewer.Mcp.Models.Context.Columnstore;
using InternalsViewer.Mcp.Models.Context.Indexes;
using InternalsViewer.Mcp.Models.Context.Pages;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Tabs;

namespace InternalsViewer.Mcp.Tests.Documents;

internal sealed class TestAreaTab(McpArea area, TabContext? context = null, object? commands = null) : IMcpTab, IMcpTabSource
{
    public string Id { get; init; } = "t1";

    public string Title { get; init; } = "Test";

    public McpArea Area { get; } = area;

    public bool IsActive { get; init; } = true;

    public string ServerName { get; init; } = @".\SQL2025";

    public string DatabaseName { get; init; } = "Sales";

    public DatabaseSource? Database { get; init; }

    public bool Activated { get; private set; }

    public bool Closed { get; private set; }

    private TabContext? Context { get; } = context;

    public Task<List<IMcpTab>> GetTabsAsync(CancellationToken cancellationToken) => Task.FromResult(new List<IMcpTab> { this });

    public Task<TabContext> GetContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Context ?? Empty());

    public Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        Activated = true;

        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        Closed = true;

        return Task.CompletedTask;
    }

    public Task<string> RunAsync<TCommands>(Func<TCommands, Task<string>> command, CancellationToken cancellationToken = default)
        where TCommands : class
        => commands is TCommands target
            ? command(target)
            : throw new NotSupportedException($"No {typeof(TCommands).Name} commands.");

    private TabContext Empty()
        => Area switch
        {
            McpArea.Allocations => new AllocationsContext { TabId = Id, Title = Title, Database = Database },
            McpArea.Page => new PageContext { TabId = Id, Title = Title, Database = Database },
            McpArea.Index => new IndexContext { TabId = Id, Title = Title, Database = Database },
            McpArea.Columnstore => new ColumnstoreContext { TabId = Id, Title = Title, Database = Database },
            _ => new QueryContext { TabId = Id, Title = Title, Database = Database }
        };
}