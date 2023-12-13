using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IBootPageService
{
    Task<BootPage> Load(Database database);
}