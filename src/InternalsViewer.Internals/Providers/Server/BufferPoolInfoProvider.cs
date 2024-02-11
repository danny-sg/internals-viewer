using System.Data;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Providers.Server;

public class BufferPoolInfoProvider(string connectionString): IBufferPoolInfoProvider
{
    private string ConnectionString { get; } = connectionString;

    private const string BufferPoolCommand = @" -- Query Buffer Pool
        SELECT CONVERT(SMALLINT, file_id) AS FileId
              ,page_id                    AS PageId
              ,is_modified                AS IsModified
        FROM   sys.dm_os_buffer_descriptors WITH (NOLOCK)
        WHERE  database_id = DB_ID(@DatabaseName)";

    public async Task<(List<PageAddress> Clean, List<PageAddress> Dirty)> GetBufferPoolEntries(string databaseName)
    {
        var dirtyPages = new List<PageAddress>();
        var cleanPages = new List<PageAddress>();

        await using var connection = new SqlConnection(ConnectionString);

        var command = new SqlCommand(BufferPoolCommand, connection);

        command.CommandType = CommandType.Text;

        command.Parameters.AddWithValue("@DatabaseName", databaseName);

        await connection.OpenAsync();

        await connection.ChangeDatabaseAsync("master");

        var reader = await command.ExecuteReaderAsync();

        while (reader.Read())
        {
            var fileId = reader.GetInt16(0);
            var pageId = reader.GetInt32(1);

            var pageAddress = new PageAddress(fileId, pageId);

            if (reader.GetBoolean(2))
            {
                dirtyPages.Add(pageAddress);
            }

            else
            {
                cleanPages.Add(pageAddress);
            }
        }

        await connection.CloseAsync();

        return (cleanPages, dirtyPages);
    }
}