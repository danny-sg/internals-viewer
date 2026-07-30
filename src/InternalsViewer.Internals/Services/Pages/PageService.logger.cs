using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Internals.Services.Pages;

public partial class PageService
{
    [LoggerMessage(LogLevel.Debug, "Loading page {PageAddress}")]
    static partial void LogLoadPage(ILogger<PageService> logger, PageAddress pageAddress);

    [LoggerMessage(LogLevel.Debug, "Loading page {PageAddress} into buffer")]
    static partial void LogLoadBufferPage(ILogger<PageService> logger, PageAddress pageAddress);

    [LoggerMessage(LogLevel.Debug, "Page {PageAddress}: Page Type: {PageType}")]
    static partial void LogPageType(ILogger<PageService> logger, PageAddress pageAddress, PageType pageType);
}