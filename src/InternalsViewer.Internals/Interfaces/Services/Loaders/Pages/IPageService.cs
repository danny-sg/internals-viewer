using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

public interface IPageService
{
    Task<Page> GetPage(DatabaseSource database, PageAddress pageAddress, CancellationToken cancellationToken, bool isMarkEnabled = true);

    Task<Page> GetPage(DatabaseSource database, 
                       PageAddress pageAddress, 
                       byte[] buffer, 
                       CancellationToken cancellationToken);

    Task<T> GetPage<T>(DatabaseSource database, PageAddress pageAddress, CancellationToken cancellationToken, bool isMarkEnabled = true) 
        where T : Page;

    void ResetCache(DatabaseSource database);
}
