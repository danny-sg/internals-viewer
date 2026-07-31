using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Services.Pages;

public partial class CachingPageService
{
    [LoggerMessage(LogLevel.Trace, "Cache hit: {PageAddress}")]
    static partial void LogCacheHit(ILogger<CachingPageService> logger, PageAddress pageAddress);

    [LoggerMessage(LogLevel.Debug, "Page cache reset for database {DatabaseName}")]
    static partial void LogCacheReset(ILogger<CachingPageService> logger, string databaseName);
}
