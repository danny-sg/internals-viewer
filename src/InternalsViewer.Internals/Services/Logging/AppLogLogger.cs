namespace InternalsViewer.Internals.Services.Logging;

internal sealed class AppLogLogger(string categoryName, AppLogService appLogService) : ILogger
{
    private string? CurrentScope { get; set; }

    public bool IsEnabled(LogLevel logLevel) => logLevel >= appLogService.MinimumLevel;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var previous = CurrentScope;
        CurrentScope = state.ToString();
        return new ScopeDisposable(() => CurrentScope = previous);
    }

    public void Log<TState>(LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var parameters = state as IReadOnlyList<KeyValuePair<string, object?>>;

        appLogService.Publish(new LogEntry(categoryName,
            logLevel,
            formatter(state, exception),
            exception,
            DateTime.Now,
            CurrentScope,
            parameters));
    }

    private sealed class ScopeDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}