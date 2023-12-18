using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IAllocationChainService
{
    Task<AllocationChain> LoadChain(DatabaseDetail databaseDetail, short fileId, PageType pageType);

    Task<AllocationChain> LoadChain(DatabaseDetail databaseDetail, PageAddress startPageAddress);
}