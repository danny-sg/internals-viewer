using System.Data;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Metadata;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Providers.Server;

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
            currentColumn.DataType = SqlTypeConverter.ToSqlType(reader.GetFieldValue<byte>("system_type_id"));
            currentColumn.DataLength = reader.GetFieldValue<short>("max_length");

            currentColumn.Key = reader.GetFieldValue<bool>("IsKey");
            currentColumn.IsUniqueifer = reader.GetFieldValue<bool>("is_uniqueifier");
            currentColumn.IsDropped = reader.GetFieldValue<bool>("is_dropped");

            indexStructure.Columns.Add(currentColumn);
        }

        return indexStructure;
    }

    public async Task<CompressionType> GetCompressionType(long allocationUnitId)
    {
        var parameters = new SqlParameter[]
        {
            new("@AllocationUnitId", allocationUnitId)
        };

        return await GetScalar<CompressionType>(SqlCommands.Compression, parameters);
    }
}