using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Readers.Pages;

public partial class QueryPageReader
{
    [LoggerMessage(LogLevel.Debug, "Reading page {PageAddress}: {CommandSql}")]
    static partial void LogReadingPage(ILogger<QueryPageReader> logger,
                                       PageAddress pageAddress,
                                       string commandSql);

    [LoggerMessage(LogLevel.Debug, "Page loaded in {Duration}")]
    static partial void LogPageLoaded(ILogger<QueryPageReader> logger, TimeSpan duration);
}
