using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using Microsoft.Data.SqlClient;
using System.Data;

namespace InternalsViewer.Internals.Providers.Metadata;

public class ServerInfoProvider(CurrentConnection connection)
    : ProviderBase(connection), IServerInfoProvider
{
    public async Task<List<DatabaseSummary>> GetDatabases()
    {
        var databases = new List<DatabaseSummary>();

        await using var connection = new SqlConnection(Connection.ConnectionString);

        var command = new SqlCommand(SqlCommands.Databases, connection);

        command.CommandType = CommandType.Text;

        await connection.OpenAsync();

        await connection.ChangeDatabaseAsync("master");

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var database = new DatabaseSummary();

            database.Name = reader.GetFieldValue<string>("name");
            database.State = reader.GetFieldValue<DatabaseState>("state");
            database.CompatibilityLevel = reader.GetFieldValue<byte>("compatibility_Level");
            database.DatabaseId = reader.GetFieldValue<int>("database_id");

            databases.Add(database);
        }

        return databases;
    }

    public async Task<DatabaseSummary?> GetDatabase(string name)
    {
        var databases = await GetDatabases();

        return databases.FirstOrDefault(d => d.Name == name);
    }
}
