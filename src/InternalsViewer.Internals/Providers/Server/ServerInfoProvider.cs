using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using Microsoft.Data.SqlClient;
using System.Data;

namespace InternalsViewer.Internals.Providers.Server;

public class ServerInfoProvider(CurrentConnection connection)
    : IServerInfoProvider
{
    public CurrentConnection Connection { get; } = connection;

    public static readonly string DatabasesCommand =
        @"-- Query Databases
        SELECT d.database_id
        	  ,d.name
              ,d.state 
              ,d.compatibility_level
        FROM   sys.databases d  
        ORDER BY d.name";

    public async Task<List<DatabaseSummary>> GetDatabases()
    {
        var databases = new List<DatabaseSummary>();

        await using var connection = new SqlConnection(Connection.ConnectionString);

        var command = new SqlCommand(DatabasesCommand, connection);

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
            database.DatabaseId = (short)reader.GetFieldValue<int>("database_id");

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
