using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Export;

namespace InternalsViewer.Mcp.Interfaces.Documents;

public interface IMcpDocument
{
    string Id { get; }

    string Title { get; }

    string ServerName { get; }

    string DatabaseName { get; }

    bool IsActive { get; }

    string SqlText { get; }

    QueryExportContent? Run { get; }

    DatabaseSource? Database { get; }

    IMcpNavigator? Navigator { get; }
}
