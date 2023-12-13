using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IPfsChainService
{
    Task<PfsChain> LoadChain(Database database, int fileId);
}