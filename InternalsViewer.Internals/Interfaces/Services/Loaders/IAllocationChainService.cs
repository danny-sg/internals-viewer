using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IAllocationChainService
{
    Task<AllocationChain> LoadChain(Database database, short fileId, PageType pageType);

    Task<AllocationChain> LoadChain(Database database, PageAddress startPageAddress);
}