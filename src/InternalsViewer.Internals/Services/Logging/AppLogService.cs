namespace InternalsViewer.Internals.Services.Logging;

public sealed class AppLogService
{
    public event Action<LogEntry>? LogEntryReceived;

    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    internal void Publish(LogEntry entry) => LogEntryReceived?.Invoke(entry);
}