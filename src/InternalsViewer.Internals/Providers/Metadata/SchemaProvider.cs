using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Providers.Metadata;

public static class SchemaProvider
{
    public static Dictionary<int, string> GetSchemas(DatabaseSource database)
    {
        return database.Metadata
                       .Entities
                       .Where(s => s.ClassId == MetadataConstants.SchemaClassId)
                       .ToDictionary(s => s.Id, s => s.Name);
    }
}
