using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IAllocationPageService
{
    Task<AllocationPage> Load(DatabaseDetail databaseDetail, PageAddress pageAddress);
}