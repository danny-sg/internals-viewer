using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IPageService
{
    Task<T> Load<T>(DatabaseDetail databaseDetail, PageAddress pageAddress) where T : Page, new();
}