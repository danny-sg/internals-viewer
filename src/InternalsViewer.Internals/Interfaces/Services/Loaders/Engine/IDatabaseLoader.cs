using InternalsViewer.Internals.Connections;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Connections;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;

public interface IDatabaseLoader
{
    Task<DatabaseSource> Load(string name, IConnectionType connection);
}