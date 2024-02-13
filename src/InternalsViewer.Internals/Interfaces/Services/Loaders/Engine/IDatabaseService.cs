using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Connections;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;

public interface IDatabaseService
{
    Task<DatabaseSource> LoadAsync(string name, IConnectionType connection);
}