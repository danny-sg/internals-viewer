using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Internals;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;

public interface IMetadataLoader
{
    Task<InternalMetadata> Load(DatabaseSource database);
}