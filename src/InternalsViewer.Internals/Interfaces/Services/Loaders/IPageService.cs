using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using System.Threading.Tasks;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IPageService
{
    Task<T> Load<T>(Database database, PageAddress pageAddress) where T : Page, new();
}