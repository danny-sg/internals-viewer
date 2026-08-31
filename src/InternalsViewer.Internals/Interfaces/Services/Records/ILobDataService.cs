using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Services.Records;

/// <summary>
/// Service responsible for retrieving LOB data by following blob structures across pages
/// </summary>
public interface ILobDataService
{
    Task<byte[]> GetData(DatabaseSource database,
                         RowIdentifier rowIdentifier,
                         CancellationToken cancellationToken,
                         Action<PageAddress>? onPageRead = null);

    /// <summary>
    /// Reads no more than the opening bytes of a blob, along with the length the whole blob would have been
    /// </summary>
    /// <remarks>
    /// A blob spanning many pages costs a page read per chunk, so a caller that only needs a header stops the walk
    /// as soon as it has the bytes it asked for.
    /// </remarks>
    Task<LobDataPrefix> GetDataPrefix(DatabaseSource database,
                                      RowIdentifier rowIdentifier,
                                      int maxLength,
                                      CancellationToken cancellationToken,
                                      Action<PageAddress>? onPageRead = null);
}
