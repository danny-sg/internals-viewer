using InternalsViewer.Internals.Tests.VerificationTool.Helpers;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Tests.VerificationTool.Services;

internal class ObjectService
{
    public async Task<List<(int ObjectId, int IndexId)>> GetIndexes()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("Default");

        await using var sqlConnection = new SqlConnection(connectionString);

        var results = new List<(int ObjectId, int IndexId)>();

        var sql = @"
            SELECT i.object_id, i.index_id
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

            results.Add((objectId, indexId));
        }

        return results;
    }

    public async Task<List<int>> GetTables()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("Default");

        await using var sqlConnection = new SqlConnection(connectionString);

        var results = new List<int>();

        var sql = @"
            SELECT o.object_id
            FROM   sys.objects o
            WHERE  o.type IN ('U', 'S')";

        await using var command = new SqlCommand(sql, sqlConnection);

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