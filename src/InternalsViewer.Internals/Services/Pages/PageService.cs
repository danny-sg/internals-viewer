﻿using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Services.Pages;

public class PageService(ILogger<PageService> logger, 
                         IPageLoader loader, 
                         IEnumerable<IPageParser> parsers) : IPageService
{
    public ILogger<PageService> Logger { get; } = logger;

    public async Task<Page> GetPage(DatabaseDetail database, PageAddress pageAddress)
    {
        using var _ = Logger.BeginScope($"PageService.GetPage: {pageAddress}");

        Logger.LogDebug($"Loading page {pageAddress}");

        var page = await loader.Load(database, pageAddress);

        var parser = parsers.FirstOrDefault(p => p.SupportedPageTypes.Any(t => page.PageHeader.PageType == t));

        if (parser == null)
        {
            throw new ArgumentException($"No parser found for page type {page.PageHeader.PageType}");
        }

        Logger.LogDebug($"Using Parser: {parser.GetType()}");

        return parser.Parse(page);
    }

    public async Task<T> GetPage<T>(DatabaseDetail database, PageAddress pageAddress) where T : Page
    {
        var page = await GetPage(database, pageAddress);

        if (page is not T typedPage)
        {
            throw new ArgumentException($"Page is not of type {typeof(T)}");
        }

        return typedPage;
    }
}