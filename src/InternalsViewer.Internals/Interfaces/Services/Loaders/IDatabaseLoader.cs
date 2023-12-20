using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IDatabaseLoader
{
    Task<DatabaseDetail> Load(string name);
}