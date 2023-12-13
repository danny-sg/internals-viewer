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
            var fileId = reader.GetFieldValue<int>("file_id");

            var file = new DatabaseFile(fileId);

            file.FileGroup = reader.GetFieldValue<string>("filegroup_name");
            file.Name = reader.GetFieldValue<string>("name");
            file.PhysicalName = reader.GetFieldValue<string>("physical_name");
            file.Size = reader.GetFieldValue<int>("size");
            file.TotalExtents = reader.GetFieldValue<int>("total_extents");
            file.UsedExtents = reader.GetFieldValue<int>("used_extents");

            files.Add(file);
        }

        return files;
    }

    public async Task<int> GetFileSize(int fileId)
    {
        var result = await GetScalar<int>(SqlCommands.FileSize,
                                          new[] { new SqlParameter("@FileId", fileId) });

        return result;
    }
}