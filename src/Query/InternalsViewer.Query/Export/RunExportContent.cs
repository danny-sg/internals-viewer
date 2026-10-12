namespace InternalsViewer.Query.Export;

public sealed record RunExportContent
{
    public required string ServerName { get; init; }

    public required string DatabaseName { get; init; }

    public required string SqlText { get; init; }

    public QueryOptions QueryOptions { get; init; } = new();

    public EventOptions EventOptions { get; init; } = new();

    public IReadOnlyList<string> Messages { get; init; } = [];

    public required QueryResult Result { get; init; }

    public IReadOnlyList<IRunExtra> Extras { get; init; } = [];

    public DateTimeOffset? ExportedAt { get; init; }

    public T? ExtraOf<T>() where T : class, IRunExtra => Extras.OfType<T>().FirstOrDefault();
}
