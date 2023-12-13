using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IAllocationChainService
{
    Task<AllocationChain> LoadChain(Database database, int fileId, PageType pageType);

    Task<AllocationChain> LoadChain(Database database, PageAddress startPageAddress);
}

public interface IPfsChainService
{
    Task<PfsChain> LoadChain(Database database, int fileId);
}

public interface IIamChainService
{
    Task<AllocationChain> LoadChain(Database database, PageAddress startPageAddress);
}