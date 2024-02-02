using InternalsViewer.Internals.Engine.Address;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Tests.VerificationTool.Helpers;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Tests.VerificationTool.Services;

internal class ObjectPageListService
{
    public async Task<List<PageAddress>> GetPages(int objectId, int indexId)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("Default");

        await using var sqlConnection = new SqlConnection(connectionString);

        var results = new List<PageAddress>();

        var sql = @"SELECT allocated_page_file_id, allocated_page_page_id
                    FROM   sys.dm_db_database_page_allocations(DB_ID(), @ObjectId, @IndexId, NULL, 'DETAILED')";

        await using var command = new SqlCommand(sql, sqlConnection);

        command.Parameters.AddWithValue("@ObjectId", objectId);
        command.Parameters.AddWithValue("@IndexId", indexId);

        await sqlConnection.OpenAsync();

        var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var fileId = reader.GetInt16(0);
            var pageId = reader.GetInt32(1);

            results.Add(new PageAddress(fileId, pageId));
        }

        return results;
    }
}
