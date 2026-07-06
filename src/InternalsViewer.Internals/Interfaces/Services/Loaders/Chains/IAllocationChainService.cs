using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;

public interface IAllocationChainService
{
    Task<AllocationChain> LoadChain(DatabaseSource database, 
                                    short fileId, 
                                    PageType pageType, 
                                    CancellationToken cancellationToken);

    Task<AllocationChain> LoadChain(DatabaseSource database, 
                                    PageAddress startPageAddress, 
                                    CancellationToken cancellationToken);
}