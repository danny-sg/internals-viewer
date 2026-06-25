using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

public interface IPageService
{
    Task<Page> GetPage(DatabaseSource database, PageAddress pageAddress);

    Task<Page> GetPage(DatabaseSource database, PageAddress pageAddress, byte[] buffer);

    Task<T> GetPage<T>(DatabaseSource database, PageAddress pageAddress) where T : Page;

    void ResetCache(DatabaseSource database);
}
