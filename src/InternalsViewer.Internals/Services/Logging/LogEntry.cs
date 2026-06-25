namespace InternalsViewer.Internals.Services.Logging;

public sealed record LogEntry(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception,
    DateTime Timestamp,
    string? Scope,
    IReadOnlyList<KeyValuePair<string, object?>>? Parameters = null)
{
    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

    public string ShortCategory => Category.Contains('.')
        ? Category[(Category.LastIndexOf('.') + 1)..]
        : Category;
}