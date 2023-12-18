using InternalsViewer.Internals.Engine.Address;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Interfaces.Readers;

public interface IPageReader
{
    Task<byte[]> Read(string databaseName, PageAddress pageAddress);
}
