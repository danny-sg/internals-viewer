using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;

public interface IIamChainService
{
    Task<IamChain> LoadChain(DatabaseSource database, PageAddress startPageAddress);
}