using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

public interface IPageLoader
{
    Task<PageData> Load(DatabaseDetail database, PageAddress pageAddress);
}