using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages;

/// <summary>
/// Decorator for <see cref="IPageService"/> that caches structural pages for the lifetime of a
/// <see cref="DatabaseSource"/>. Allocation and system pages (IAM, GAM, SGAM, DCM, BCM, Boot,
/// FileHeader) are stable for the duration of a session and benefit most from caching.
/// </summary>
public sealed class CachingPageService(ILogger<CachingPageService> logger, PageService inner) : IPageService
{
    private static readonly HashSet<PageType> CacheablePageTypes =
    [
        PageType.Iam,
        PageType.Gam,
        PageType.Sgam,
        PageType.Dcm,
        PageType.Bcm,
        PageType.Boot,
        PageType.FileHeader,
        PageType.Pfs
    ];

    // Weak keys so each per-database cache is collected automatically when the DatabaseSource is GC'd.
    // ConcurrentDictionary so pages can be loaded in parallel (e.g. IAM chains during a database
    // refresh); a race just re-loads the same page and the last write wins with identical data.
    private readonly ConditionalWeakTable<DatabaseSource, ConcurrentDictionary<PageAddress, Page>> _cache = new();

    private ILogger<CachingPageService> Logger { get; } = logger;

    public async Task<Page> GetPage(DatabaseSource database, PageAddress pageAddress)
    {
        var dbCache = _cache.GetOrCreateValue(database);

        if (dbCache.TryGetValue(pageAddress, out var cached))
        {
            Logger.LogTrace("Cache hit: {PageAddress}", pageAddress);

            return cached;
        }

        var page = await inner.GetPage(database, pageAddress);

        if (CacheablePageTypes.Contains(page.PageHeader.PageType))
        {
            dbCache[pageAddress] = page;
        }

        return page;
    }

    public async Task<Page> GetPage(DatabaseSource database, PageAddress pageAddress, byte[] buffer)
    {
        var dbCache = _cache.GetOrCreateValue(database);

        if (dbCache.TryGetValue(pageAddress, out var cached))
        {
            Logger.LogTrace("Cache hit: {PageAddress}", pageAddress);

            return cached;
        }

        var page = await inner.GetPage(database, pageAddress, buffer);

        // If this turns out to be a cacheable type the page's Data points at the caller's reusable
        // buffer, which would be overwritten on the next traversal step. Re-read with an owned
        // allocation so the cached copy is stable.
        if (CacheablePageTypes.Contains(page.PageHeader.PageType))
        {
            var ownedPage = await inner.GetPage(database, pageAddress);

            dbCache[pageAddress] = ownedPage;

            return ownedPage;
        }

        return page;
    }

    public async Task<T> GetPage<T>(DatabaseSource database, PageAddress pageAddress)
        where T : Page
    {
        var page = await GetPage(database, pageAddress);

        if (page is not T typedPage)
        {
            throw new ArgumentException($"Page is not of type {typeof(T)}");
        }

        return typedPage;
    }

    /// <summary>
    /// Removes all cached pages for the given database, forcing fresh reads on the next access.
    /// Call before any refresh operation that re-reads structural pages.
    /// </summary>
    public void ResetCache(DatabaseSource database)
    {
        _cache.Remove(database);

        Logger.LogDebug("Page cache reset for database {DatabaseName}", database.Name);
    }
}
