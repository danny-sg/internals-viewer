using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IPageLoader
{
    Task<T> Load<T>(DatabaseDetail databaseDetail, PageAddress pageAddress) where T : Page, new();
}