using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Services.Indexes;

public partial class IndexService
{
    [LoggerMessage(LogLevel.Debug,
                   "Page {PageAddress} has multiple parents - keeping {Parent}, ignoring {Ignored}")]
    static partial void LogMultipleParents(ILogger<IndexService> logger,
                                           PageAddress pageAddress,
                                           PageAddress parent,
                                           PageAddress ignored);
}
