using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Providers.Metadata;

public class BufferPoolInfoProvider(CurrentConnection connection): ProviderBase(connection), IBufferPoolInfoProvider
{
    public async Task<(List<PageAddress> Clean, List<PageAddress> Dirty)> GetBufferPoolEntries(string databaseName)
    {
        var dirtyPages = new List<PageAddress>();
        var cleanPages = new List<PageAddress>();

        await using var connection = new SqlConnection(Connection.ConnectionString);

        var command = new SqlCommand(SqlCommands.BufferPool, connection);

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