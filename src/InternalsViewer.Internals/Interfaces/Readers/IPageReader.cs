using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Interfaces.Readers;

public interface IPageReader
{
    Task<byte[]> Read(string databaseName, PageAddress pageAddress);
}
