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
                         CancellationToken cancellationToken);
}
