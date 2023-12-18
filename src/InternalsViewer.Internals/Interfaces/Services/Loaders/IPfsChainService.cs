using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IPfsChainService
{
    Task<PfsChain> LoadChain(DatabaseDetail databaseDetail, short fileId);
}