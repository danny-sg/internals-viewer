using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Connections;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;

public interface IDatabaseService
{
    Task<DatabaseSource> Load(string name, IConnectionType connection);
}