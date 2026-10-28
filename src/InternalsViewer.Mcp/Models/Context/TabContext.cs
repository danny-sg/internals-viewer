using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Mcp.Models.Context;

public abstract record TabContext
{
    public required string TabId { get; init; }

    public required string Title { get; init; }

    public DatabaseSource? Database { get; init; }
}
