using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Pages.Loaders;

namespace InternalsViewer.Internals.Services.Pages;

/// <summary>
/// Service responsible for getting and parsing pages
/// </summary>
public sealed class PageService(ILogger<PageService> logger,
                                IPageLoader loader,
                                IEnumerable<IPageParser> parsers) : IPageService
{
    private ILogger<PageService> Logger { get; } = logger;

    private Dictionary<PageType, IPageParser> Parsers { get; } =
        parsers.SelectMany(p => p.SupportedPageTypes.Select(t => (PageType: t, Parser: p)))
               .ToDictionary(t => t.PageType, t => t.Parser);

    public async Task<Page> GetPage(DatabaseSource database,
                                    PageAddress pageAddress,
                                    CancellationToken cancellationToken,
                                    bool isMarkEnabled = true)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Loading page {PageAddress}", pageAddress);
        }

        var page = await loader.Load(database, pageAddress, cancellationToken, isMarkEnabled);

        return ParsePage(page, pageAddress);
    }

    public async Task<Page> GetPage(DatabaseSource database,
                                    PageAddress pageAddress,
                                    byte[] buffer,
                                    CancellationToken cancellationToken)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Loading page {PageAddress} into buffer", pageAddress);
        }

        var page = await loader.LoadInto(database, pageAddress, buffer, cancellationToken);

        return ParsePage(page, pageAddress);
    }

    public async Task<T> GetPage<T>(DatabaseSource database,
                                    PageAddress pageAddress,
                                    CancellationToken cancellationToken,
                                    bool isMarkEnabled = true)
        where T : Page
    {
        var page = await GetPage(database, pageAddress, cancellationToken, isMarkEnabled);

        if (page is not T typedPage)
        {
            throw new ArgumentException($"Page is not of type {typeof(T)}");
        }

        return typedPage;
    }

    public Page ParsePage(DatabaseSource database, PageAddress pageAddress, byte[] data)
    {
        var page = PageLoader.BuildPageData(database, pageAddress, data, true);

        return ParsePage(page, pageAddress);
    }

    public void ResetCache(DatabaseSource database)
    {
    }

    private Page ParsePage(PageData page, PageAddress pageAddress)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Page {PageAddress}: Page Type: {PageType}", pageAddress, page.PageHeader.PageType);
        }

        if (!Parsers.TryGetValue(page.PageHeader.PageType, out var parser))
        {
            Logger.LogError("Page {PageAddress}: Page Type: {PageType} - No parser found for page type",
                            pageAddress,
                            page.PageHeader.PageType);

            throw new ArgumentException($"No parser found for page type {page.PageHeader.PageType}");
        }

        return parser.Parse(page);
    }
}