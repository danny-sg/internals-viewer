using System.Threading;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Engine.Loading;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;

public interface IDatabaseService
{
    Task<DatabaseSource> LoadAsync(string name,
                                   IConnectionType connection,
                                   CancellationToken cancellationToken,
                                   IProgress<ProgressDetail>? progress = null);
}