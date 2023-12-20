using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.MetadataProviders;

public interface IServerInfoProvider
{
    Task<List<DatabaseSummary>> GetDatabases();

    Task<DatabaseSummary?> GetDatabase(string name);
}