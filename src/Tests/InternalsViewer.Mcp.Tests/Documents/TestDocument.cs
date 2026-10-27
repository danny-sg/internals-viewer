using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Query.Export;

namespace InternalsViewer.Mcp.Tests.Documents;

internal sealed class TestDocument : IMcpTab
{
    public string Id { get; init; } = "q1";

    public string Title { get; init; } = "Sales: Query";

    public McpArea Area => McpArea.Query;

    public string ServerName { get; init; } = @".\SQL2025";

    public string DatabaseName { get; init; } = "Sales";

    public bool IsActive { get; init; } = true;

    public string SqlText { get; init; } = "SELECT * FROM dbo.Orders";

    public QueryExportContent? Run { get; init; }

    public DatabaseSource? Database { get; init; }

    public RecordingNavigator Recorded { get; init; } = new();

    public Task<TabContext> GetContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<TabContext>(new QueryContext
        {
            TabId = Id,
            Title = Title,
            Database = Database,
            Sql = SqlText,
            Run = Run,
            Selection = Recorded.Selection
        });

    public Task<string> RunAsync<TCommands>(Func<TCommands, Task<string>> command, CancellationToken cancellationToken = default)
        where TCommands : class
        => Recorded is TCommands target
            ? command(target)
            : throw new NotSupportedException($"No {typeof(TCommands).Name} commands.");
}