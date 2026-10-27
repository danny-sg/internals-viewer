using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Query.Export;

namespace InternalsViewer.Mcp.Tests.Documents;

internal sealed class TestDocument : IMcpDocument
{
    public string Id { get; init; } = "q1";

    public string Title { get; init; } = "Sales: Query";

    public string ServerName { get; init; } = @".\SQL2025";

    public string DatabaseName { get; init; } = "Sales";

    public bool IsActive { get; init; } = true;

    public string SqlText { get; init; } = "SELECT * FROM dbo.Orders";

    public QueryExportContent? Run { get; init; }

    public DatabaseSource? Database { get; init; }

    public IMcpNavigator? Navigator { get; init; } = new RecordingNavigator();

    public RecordingNavigator Recorded => (RecordingNavigator)Navigator!;
}
