using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Loading;

namespace InternalsViewer.Internals.Interfaces.Readers;

public interface IPageReader : IAsyncDisposable
{
    Task Initialize(CancellationToken cancellationToken, IProgress<ProgressDetail>? progress = null);

    Task<byte[]> Read(string name, PageAddress pageAddress, CancellationToken cancellationToken);

    Task ReadInto(string name, PageAddress pageAddress, byte[] buffer, CancellationToken cancellationToken);
}
