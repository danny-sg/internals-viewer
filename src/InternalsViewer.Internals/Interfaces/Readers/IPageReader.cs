using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Interfaces.Readers;

public interface IPageReader : IAsyncDisposable
{
    Task<byte[]> Read(string name, PageAddress pageAddress);

    Task ReadInto(string name, PageAddress pageAddress, byte[] buffer);
}
