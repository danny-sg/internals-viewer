using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
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

    public async Task<short> GetDatabaseId(string name)
    {
        var parameters = new SqlParameter[]
        {
            new("@DatabaseName", name)
        };

        return await GetScalar<short>(SqlCommands.DatabaseId, parameters);
    }

    public async Task<List<AllocationUnit>> GetAllocationUnits()
    {
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

            allocationUnit.ObjectId = reader.GetFieldValue<int>("object_id");
            allocationUnit.IndexId = reader.GetFieldValue<int>("index_id");
            allocationUnit.FirstIamPage = reader.GetFieldValue<byte[]>("first_iam_page");
            allocationUnit.SchemaName = reader.GetFieldValue<string>("schema_name");
            allocationUnit.TableName = reader.GetFieldValue<string>("table_name");
            allocationUnit.IndexName = reader.GetNullableValue<string?>("index_name") ?? string.Empty;
            allocationUnit.IsSystem = reader.GetFieldValue<bool>("is_system");
            allocationUnit.IndexId = reader.GetFieldValue<int>("index_id");
            allocationUnit.IndexType = reader.GetFieldValue<byte>("index_type");
            allocationUnit.AllocationUnitType = reader.GetFieldValue<AllocationUnitType>("allocation_unit_type");
            allocationUnit.UsedPages = reader.GetFieldValue<long>("used_pages");
            allocationUnit.TotalPages = reader.GetFieldValue<long>("total_pages");

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
