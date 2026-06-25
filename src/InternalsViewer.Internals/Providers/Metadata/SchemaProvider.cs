using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Providers.Metadata;

public static class SchemaProvider
{
    public static Dictionary<int, string> GetSchemas(DatabaseSource database)
    {
        return database.Metadata
                       .Entities
                       .Where(e => e.Key.ClassId == MetadataConstants.SchemaClassId)
                       .ToDictionary(e => e.Key.Id, e => e.Value.Name);
    }
}
