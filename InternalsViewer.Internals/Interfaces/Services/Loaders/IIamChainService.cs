using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IIamChainService
{
    Task<AllocationChain> LoadChain(Database database, PageAddress startPageAddress);
}