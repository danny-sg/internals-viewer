using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Interfaces.Readers;

public interface IPageReader
{
    Task<PageData> Read(string databaseName, PageAddress pageAddress);
}
