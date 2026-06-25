namespace InternalsViewer.Internals.Services.Logging;

public sealed class AppLogLoggerProvider(AppLogService progressService) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new AppLogLogger(categoryName, progressService);

    public void Dispose()
    {
    }
}