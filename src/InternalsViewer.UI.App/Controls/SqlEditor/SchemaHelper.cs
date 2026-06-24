using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.UI.App.Models.Schema;

namespace InternalsViewer.UI.App.Controls.SqlEditor;

internal class SchemaHelper
{
    public static DatabaseSchema ToSqlSchema(DatabaseSource database)
    {
        var databaseSchema = new DatabaseSchema();

        var schemas = SchemaProvider.GetSchemas(database);

        databaseSchema.Schemas = schemas.Select(s => new SqlSchema { Name = s.Value }).ToList();

        var tables = database.Metadata.Objects.Where(o => (o.ObjectType == "U " || o.ObjectType == "V ") 
                                                          && (o.Status & 1) == 0);

        foreach (var internalObject in tables)
        {
            databaseSchema.Tables.Add(new SqlTable
            {
                Name = internalObject.Name,
                Schema = schemas[internalObject.SchemaId]
            });

            var columns = database.Metadata.Columns.Where(c => c.ObjectId == internalObject.ObjectId);

            databaseSchema.Columns.AddRange(columns.Select(
                c => new SqlColumn
                {
                    Name = c.Name ?? "UNKNOWN",
                    Table = internalObject.Name,
                    Schema = schemas[internalObject.SchemaId]
                }));
        }

        return databaseSchema;
    }
}
