using System.Threading;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Interfaces.Readers;

public interface IPageReader : IAsyncDisposable
{
    Task Initialize(CancellationToken cancellationToken);

    Task<byte[]> Read(string name, PageAddress pageAddress, CancellationToken cancellationToken);

    Task ReadInto(string name, PageAddress pageAddress, byte[] buffer, CancellationToken cancellationToken);
}
