using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Metadata;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Providers.Metadata;

public class StructureInfoProvider(CurrentConnection connection) : ProviderBase(connection), IStructureInfoProvider
{
    public async Task<IndexStructure> GetIndexStructure(long allocationUnitId)
    {
        var indexStructure = new IndexStructure(allocationUnitId);

        await using var connection = new SqlConnection(Connection.ConnectionString);

        var command = new SqlCommand(SqlCommands.IndexColumns, connection);

        command.CommandType = CommandType.Text;

        await connection.OpenAsync();

        await connection.ChangeDatabaseAsync(Connection.DatabaseName);

        command.Parameters.AddWithValue("@AllocationUnitId", allocationUnitId);

        await using var reader = await command.ExecuteReaderAsync();

        var first = true;

        while (await reader.ReadAsync())
        {
            if (first)
            {
                indexStructure.IsUnique = reader.GetFieldValue<bool>("is_unique");
                indexStructure.IndexType = reader.GetFieldValue<byte>("type");
                indexStructure.IsHeap = reader.GetFieldValue<int>("hasClusteredIndex") != 1;

                first = false;
            }

            var currentColumn = new IndexColumnStructure();

            var indexId = reader.GetFieldValue<int>("index_id");

            var internalOffset = reader.GetFieldValue<short>("internal_offset");
            var leafOffset = reader.GetFieldValue<short>("leaf_offset");

            currentColumn.LeafOffset = indexId == 1 ? internalOffset : leafOffset;

            currentColumn.ColumnName = reader.GetFieldValue<string>("name");
            currentColumn.IndexColumnId = reader.GetFieldValue<int>("index_column_id");
            currentColumn.ColumnId = reader.GetFieldValue<short>("internal_null_bit");
            currentColumn.IncludedColumn = reader.GetFieldValue<bool>("is_included_column");
            currentColumn.DataType = DataConverter.ToSqlType(reader.GetFieldValue<byte>("system_type_id"));
            currentColumn.DataLength = reader.GetFieldValue<short>("max_length");

            currentColumn.Key = reader.GetFieldValue<bool>("IsKey");
            currentColumn.IsUniqueifer = reader.GetFieldValue<bool>("is_uniqueifier");
            currentColumn.IsDropped = reader.GetFieldValue<bool>("is_dropped");

            indexStructure.Columns.Add(currentColumn);
        }

        return indexStructure;
    }

    public async Task<StructureType> GetStructureType(string name)
    {
        var parameters = new SqlParameter[]
        {
            new("@TableName", name)
        };

        var hasClusteredIndex = await GetScalar<int>(SqlCommands.ObjectHasClusteredIndex, parameters);

        if (hasClusteredIndex > 0)
        {
            return StructureType.BTree;
        }

        return StructureType.Heap;
    }

    public async Task<List<HobtEntryPoint>> GetEntryPoints(string objectName, string indexName)
    {
        var entryPoints = new List<HobtEntryPoint>();

        await using var connection = new SqlConnection(Connection.ConnectionString);

        await connection.OpenAsync();

        await connection.ChangeDatabaseAsync(Connection.DatabaseName);

        var command = new SqlCommand(SqlCommands.EntryPoints, connection);

        command.CommandType = CommandType.Text;

        command.Parameters.AddWithValue("@ObjectName", objectName);
        command.Parameters.AddWithValue("@IndexName", indexName);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var firstIam = PageAddressParser.Parse(reader.GetFieldValue<byte[]>("first_iam_page"));

            var rootPage = PageAddressParser.Parse(reader.GetFieldValue<byte[]>("root_page"));
            var firstPage = PageAddressParser.Parse(reader.GetFieldValue<byte[]>("first_page"));

            var partitionNumber = reader.GetInt32("partition_number");

            entryPoints.Add(new HobtEntryPoint(firstIam, rootPage, firstPage, partitionNumber));
        }

        return entryPoints;
    }

    public async Task<CompressionType> GetCompressionType(long allocationUnitId)
    {
        var parameters = new SqlParameter[]
        {
            new("@AllocationUnitId", allocationUnitId)
        };

        return await GetScalar<CompressionType>(SqlCommands.Compression, parameters);
    }

    public async Task<string?> GetName(long allocationUnitId)
    {
        var parameters = new SqlParameter[]
        {
            new("@AllocationUnitId", allocationUnitId)
        };

        return await GetScalar<string>(SqlCommands.AllocationUnitName, parameters);
    }

    public async Task<TableStructure> GetTableStructure(long allocationUnitId)
    {
        var columns = new List<ColumnStructure>();

        await using var connection = new SqlConnection(Connection.ConnectionString);

        var command = new SqlCommand(SqlCommands.TableColumns, connection);

        command.CommandType = CommandType.Text;

        command.Parameters.AddWithValue("@AllocationUnitId", allocationUnitId);

        await connection.OpenAsync();

        await connection.ChangeDatabaseAsync(Connection.DatabaseName);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var currentColumn = new ColumnStructure();

            currentColumn.ColumnName = reader.GetFieldValue<string>("name");
            currentColumn.ColumnId = reader.GetFieldValue<int>("column_id");
            currentColumn.DataType = DataConverter.ToSqlType(reader.GetFieldValue<byte>("system_type_id"));
            currentColumn.DataLength = reader.GetFieldValue<short>("max_length");
            currentColumn.LeafOffset = reader.GetFieldValue<short>("leaf_offset");
            currentColumn.Precision = reader.GetFieldValue<byte>("precision");
            currentColumn.Scale = reader.GetFieldValue<byte>("scale");
            currentColumn.IsDropped = reader.GetFieldValue<bool>("is_dropped");
            currentColumn.IsUniqueifer = reader.GetFieldValue<bool>("is_uniqueifier");
            currentColumn.IsSparse = reader.GetFieldValue<bool>("is_sparse");
            currentColumn.NullBit = reader.GetFieldValue<short>("leaf_null_bit");

            columns.Add(currentColumn);
        }

        var tableStructure = new TableStructure(allocationUnitId)
        {
            Columns = columns,
        };

        return tableStructure;
    }
}