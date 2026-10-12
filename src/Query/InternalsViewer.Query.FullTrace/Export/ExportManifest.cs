namespace InternalsViewer.Query.FullTrace.Export;

internal sealed record ExportManifest
{
    public int FormatVersion { get; init; }

    public DateTimeOffset ExportedAt { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public long RowCount { get; init; }

    public long? CropStartUs { get; init; }

    public long? CropEndUs { get; init; }

    public QueryOptions QueryOptions { get; init; } = new();

    public EventOptions EventOptions { get; init; } = new();

    public IReadOnlyList<ExportResultSet> ResultSets { get; init; } = [];

    public IReadOnlyList<ExportPlan> Plans { get; init; } = [];

    public IReadOnlyList<ExportCallTree> CallTrees { get; init; } = [];

    public IReadOnlyList<int> ResultLogRecords { get; init; } = [];

    public bool HasFullTrace { get; init; }

    public int EventCount { get; init; }

    public int LogRecordCount { get; init; }

    public int NodeCount { get; init; }
}
