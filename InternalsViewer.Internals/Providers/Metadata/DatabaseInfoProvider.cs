using System;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Providers.Metadata;

public class DatabaseInfoProvider(CurrentConnection connection)
    : ProviderBase(connection), IDatabaseInfoProvider
{
    public async Task<List<DatabaseInfo>> GetDatabases()
    {
        var databases = new List<DatabaseInfo>();

        await using var connection = new SqlConnection(Connection.ConnectionString);

        var command = new SqlCommand(SqlCommands.Databases, connection);

        command.CommandType = CommandType.Text;

        await connection.OpenAsync();

        await connection.ChangeDatabaseAsync("master");

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var database = new DatabaseInfo();

            database.Name = reader.GetFieldValue<string>("name");
            database.State = reader.GetFieldValue<DatabaseState>("state");
            database.CompatibilityLevel = reader.GetFieldValue<byte>("compatibility_Level");
            database.DatabaseId = reader.GetFieldValue<int>("database_id");

            databases.Add(database);
        }

        return databases;
    }

    public async Task<DatabaseInfo?> GetDatabase(string name)
    {
        var databases = await GetDatabases();

        return databases.FirstOrDefault(d => d.Name == name);
    }

    public async Task<List<AllocationUnit>> GetAllocationUnits()
    {
        if(Connection.DatabaseName == null)
        {
            throw new InvalidOperationException("Database name not set");
        }   

        var allocationUnits = new List<AllocationUnit>();

        await using var connection = new SqlConnection(Connection.ConnectionString);

        var command = new SqlCommand(SqlCommands.AllocationUnits, connection);

        command.CommandType = CommandType.Text;

        await connection.OpenAsync();

        await connection.ChangeDatabaseAsync(Connection.DatabaseName);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var allocationUnit = new AllocationUnit();

            allocationUnit.AllocationUnitId = reader.GetFieldValue<long>("AllocationUnitId");

            allocationUnit.ObjectId = reader.GetFieldValue<int>("ObjectId");
            allocationUnit.IndexId = reader.GetFieldValue<int>("IndexId");

            var firstIamPage = reader.GetFieldValue<byte[]>("FirstIamPage");

            allocationUnit.FirstIamPage = PageAddressParser.Parse(firstIamPage);

            var rootPage = reader.GetFieldValue<byte[]>("RootPage");

            allocationUnit.RootPage = PageAddressParser.Parse(rootPage);

            var firstPage = reader.GetFieldValue<byte[]>("FirstPage");

            allocationUnit.FirstPage = PageAddressParser.Parse(firstPage);

            allocationUnit.SchemaName = reader.GetFieldValue<string>("SchemaName");
            allocationUnit.TableName = reader.GetFieldValue<string>("TableName");
            allocationUnit.IndexName = reader.GetNullableValue<string?>("IndexName") ?? string.Empty;
            allocationUnit.IsSystem = reader.GetFieldValue<bool>("IsSystem");
            allocationUnit.IndexId = reader.GetFieldValue<int>("IndexId");
            allocationUnit.IndexType = reader.GetFieldValue<byte>("IndexType");
            allocationUnit.AllocationUnitType = reader.GetFieldValue<AllocationUnitType>("AllocationUnitType");
            allocationUnit.UsedPages = reader.GetFieldValue<long>("UsedPages");
            allocationUnit.TotalPages = reader.GetFieldValue<long>("TotalPages");

            allocationUnits.Add(allocationUnit);
        }

        return allocationUnits;
    }

    public async Task<byte> GetCompatibilityLevel(string name)
    {
        var parameters = new SqlParameter[]
        {
            new("@name", name)
        };

        return await GetScalar<byte>(SqlCommands.CompatibilityLevel, parameters);
    }
}
