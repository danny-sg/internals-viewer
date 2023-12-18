using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IIamChainService
{
    Task<IamChain> LoadChain(DatabaseDetail databaseDetail, PageAddress startPageAddress);
}