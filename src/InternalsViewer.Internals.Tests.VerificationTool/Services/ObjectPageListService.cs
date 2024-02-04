using InternalsViewer.Internals.Engine.Address;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Tests.VerificationTool.Helpers;
using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Internals.Tests.VerificationTool.Services;

internal class ObjectPageListService
{
    public async Task<List<PageAddress>> GetPages(int objectId, int? indexId, PageType pageType, bool isCompressed = false)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("Default");

        await using var sqlConnection = new SqlConnection(connectionString);

        var results = new List<PageAddress>();

        var sql = @"SELECT allocated_page_file_id, allocated_page_page_id
                    FROM   sys.dm_db_database_page_allocations(DB_ID(), @ObjectId, @IndexId, NULL, 'DETAILED')
                    WHERE  page_type = @PageType AND is_page_compressed = @IsCompressed";

        await using var command = new SqlCommand(sql, sqlConnection);

        command.Parameters.AddWithValue("@ObjectId", objectId);
        
        if (indexId.HasValue)
        {
            command.Parameters.AddWithValue("@IndexId", indexId);
        }
        else
        {
            command.Parameters.AddWithValue("@IndexId", DBNull.Value);
        }

        command.Parameters.AddWithValue("@IsCompressed", isCompressed);

        command.Parameters.AddWithValue("@PageType", (int)pageType);

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
