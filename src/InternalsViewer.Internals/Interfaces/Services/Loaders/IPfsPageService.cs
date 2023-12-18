using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IPfsPageService
{
    Task<PfsPage> Load(Database database, PageAddress pageAddress);
}