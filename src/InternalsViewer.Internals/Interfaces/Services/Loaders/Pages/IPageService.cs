using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

public interface IPageService
{
    Task<Page> GetPage(DatabaseDetail database, PageAddress pageAddress);

    Task<T> GetPage<T>(DatabaseDetail database, PageAddress pageAddress) where T : Page;
}
