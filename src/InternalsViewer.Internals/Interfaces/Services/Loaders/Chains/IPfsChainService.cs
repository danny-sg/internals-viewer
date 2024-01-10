using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;

public interface IPfsChainService
{
    Task<PfsChain> LoadChain(DatabaseSource databaseDetail, short fileId);
}