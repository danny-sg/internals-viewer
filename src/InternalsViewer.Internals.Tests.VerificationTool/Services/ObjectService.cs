using InternalsViewer.Internals.Tests.VerificationTool.Helpers;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Tests.VerificationTool.Services;

internal class ObjectService
{
    public async Task<List<(int ObjectId, int IndexId, string Name, bool isUnique, bool isPrimaryKey, bool isUniqueConstraint)>> GetIndexes(string databaseName)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString(databaseName);

        await using var sqlConnection = new SqlConnection(connectionString);

        var results = new List<(int ObjectId, int IndexId, string Name, bool isUnique, bool isPrimaryKey, bool isUniqueConstraint)>();

        var sql = @"
            SELECT i.object_id, i.index_id, i.name, i.is_unique, i.is_primary_key, i.is_unique_constraint
            FROM   sys.indexes i 
                   INNER JOIN sys.objects o ON i.object_id = o.object_id
            WHERE  o.is_ms_shipped = 0 AND i.type IN (1, 2)";

        await using var command = new SqlCommand(sql, sqlConnection);

        await sqlConnection.OpenAsync();

        var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var objectId = reader.GetInt32(0);
            var indexId = reader.GetInt32(1);
            var name = reader.GetString(2);
            var isUnique = reader.GetBoolean(3);
            var isPrimaryKey = reader.GetBoolean(4);
            var isUniqueConstraint = reader.GetBoolean(5);

            results.Add((objectId, indexId, name, isUnique, isPrimaryKey, isUniqueConstraint));
        }

        return results;
    }

    public async Task<List<int>> GetTables(string databaseName, bool includeSystem)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString(databaseName);

        await using var sqlConnection = new SqlConnection(connectionString);

        var results = new List<int>();

        var sql = @"
            SELECT o.object_id
            FROM   sys.objects o
            WHERE  o.type IN ('U', 'S') AND (o.is_ms_shipped = 0 OR @IncludeSystem = 1)";

        await using var command = new SqlCommand(sql, sqlConnection);

        command.Parameters.AddWithValue("@IncludeSystem", includeSystem);

        await sqlConnection.OpenAsync();

        var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var objectId = reader.GetInt32(0);
   
            results.Add(objectId);
        }

        return results;
    }
}