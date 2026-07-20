using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

public interface IPageLoader
{
    Task<PageData> Load(DatabaseSource database, 
                        PageAddress pageAddress, 
                        CancellationToken cancellationToken,
                        bool isMarkEnabled = true);

    Task<PageData> LoadInto(DatabaseSource database, 
                            PageAddress pageAddress, 
                            byte[] buffer, 
                            CancellationToken cancellationToken);
}