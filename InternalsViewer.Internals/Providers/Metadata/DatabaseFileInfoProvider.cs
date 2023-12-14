using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Providers.Metadata;

public class DatabaseFileInfoProvider(CurrentConnection connection) 
    : ProviderBase(connection), IDatabaseFileInfoProvider
{
    public async Task<List<DatabaseFile>> GetFiles(string name)
    {
        var files = new List<DatabaseFile>();

        await using var connection = new SqlConnection(Connection.ConnectionString);

        await connection.OpenAsync();

        await connection.ChangeDatabaseAsync(name);

        var command = new SqlCommand(SqlCommands.Files, connection);

        command.CommandType = CommandType.Text;

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var fileId = (short)reader.GetFieldValue<int>("FileId");

            var file = new DatabaseFile(fileId);

            file.FileGroup = reader.GetFieldValue<string>("FileGroupName");
            file.Name = reader.GetFieldValue<string>("Name");
            file.PhysicalName = reader.GetFieldValue<string>("PhysicalName");
            file.Size = reader.GetFieldValue<int>("FileSize");
            file.TotalPages = reader.GetFieldValue<long>("TotalPageCount");
            file.UsedPages = reader.GetFieldValue<long>("AllocatedExtentPageCount");

            files.Add(file);
        }

        return files;
    }

    public async Task<int> GetFileSize(short fileId)
    {
        var result = await GetScalar<int>(SqlCommands.FileSize,
                                          new[] { new SqlParameter("@FileId", fileId) });

        return result;
    }
}