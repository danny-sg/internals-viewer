using System.Text;
using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Reports how the engine encoded every columnstore column of the lab database and what dictionaries it built
/// </summary>
public sealed class ColumnstoreSqlProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Dump_Hidden_Columns()
    {
        await Dump("""
                   SELECT t.name AS TableName, i.type_desc AS IndexType,
                          COUNT(DISTINCT s.column_id) AS SegmentColumns,
                          (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = t.object_id) AS TableColumns,
                          MIN(s.column_id) AS MinColumnId, MAX(s.column_id) AS MaxColumnId
                   FROM sys.column_store_segments s
                   JOIN sys.partitions p ON p.partition_id = s.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   JOIN sys.indexes i ON i.object_id = p.object_id AND i.index_id = p.index_id
                   GROUP BY t.name, t.object_id, i.type_desc
                   ORDER BY t.name
                   """);

        await Dump("""
                   SELECT TOP 20 t.name AS TableName, s.column_id AS SegmentColumnId,
                          c.name AS MatchedColumn, ty.name AS TypeName, s.encoding_type AS Encoding
                   FROM sys.column_store_segments s
                   JOIN sys.partitions p ON p.partition_id = s.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = s.column_id
                   LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                   WHERE s.segment_id = 0 AND t.name IN ('Sales', 'SegLocalDict')
                   ORDER BY t.name, s.column_id
                   """);
    }

    [RequiresConnectionStringFact("local")]
    public async Task Dump_Decimal_Encodings()
    {
        await Dump("""
                   SELECT t.name AS TableName, c.name AS ColumnName,
                          ty.name AS TypeName, c.precision, c.scale, c.max_length,
                          s.encoding_type AS Enc, d.type AS DictType, d.entry_count AS DictEntries
                   FROM sys.column_store_segments s
                   JOIN sys.partitions p ON p.partition_id = s.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = s.column_id
                   JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                   LEFT JOIN sys.column_store_dictionaries d ON d.hobt_id = s.hobt_id
                                                            AND d.column_id = s.column_id
                                                            AND d.dictionary_id = 0
                   WHERE ty.name IN ('decimal', 'numeric') AND s.segment_id = 0
                   ORDER BY c.precision, c.scale
                   """);
    }

    [RequiresConnectionStringFact("local")]
    public async Task Dump_Delete_Bitmaps()
    {
        await Dump("""
                   SELECT t.name AS TableName, rg.row_group_id, rg.state_desc, rg.total_rows, rg.deleted_rows
                   FROM sys.dm_db_column_store_row_group_physical_stats rg
                   JOIN sys.tables t ON t.object_id = rg.object_id
                   WHERE rg.deleted_rows > 0 OR t.name LIKE 'SegDel%'
                   ORDER BY t.name, rg.row_group_id
                   """);

        await Dump("""
                   SELECT t.name AS TableName, it.name AS InternalName, it.internal_type_desc,
                          p.hobt_id, au.allocation_unit_id, au.type_desc, au.total_pages
                   FROM sys.internal_tables it
                   JOIN sys.tables t ON t.object_id = it.parent_object_id
                   JOIN sys.partitions p ON p.object_id = it.object_id
                   JOIN sys.allocation_units au ON au.container_id = p.partition_id
                   WHERE it.internal_type_desc LIKE '%COLUMNSTORE%'
                   ORDER BY t.name, it.internal_type_desc
                   """);
    }

    [RequiresConnectionStringFact("local")]
    public async Task Dump_Delta_Stores()
    {
        await Dump("""
                   SELECT t.name AS TableName, rg.row_group_id, rg.state_desc, rg.total_rows, rg.deleted_rows
                   FROM sys.dm_db_column_store_row_group_physical_stats rg
                   JOIN sys.tables t ON t.object_id = rg.object_id
                   WHERE rg.state_desc <> 'COMPRESSED'
                   ORDER BY t.name, rg.row_group_id
                   """);
    }

    [RequiresConnectionStringFact("local")]
    public async Task Dump_Encodings_And_Dictionaries()
    {
        await Dump("""
                   SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS TypeName,
                          s.encoding_type AS Encoding, COUNT(*) AS Segments,
                          MAX(s.row_count) AS MaxRows, SUM(CAST(s.on_disk_size AS bigint)) AS Size
                   FROM sys.column_store_segments s
                   JOIN sys.partitions p ON p.hobt_id = s.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = s.column_id
                   JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                   GROUP BY t.name, c.name, ty.name, s.encoding_type
                   ORDER BY t.name, c.name
                   """);

        await Dump("""
                   SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS TypeName,
                          d.dictionary_id AS DictId, d.type AS DictType,
                          d.entry_count AS Entries, d.on_disk_size AS Size
                   FROM sys.column_store_dictionaries d
                   JOIN sys.partitions p ON p.hobt_id = d.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = d.column_id
                   JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                   ORDER BY t.name, c.name, d.dictionary_id
                   """);
    }

    private async Task Dump(string sql)
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };

        await using var reader = await command.ExecuteReaderAsync();

        TestOutput.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)));

        while (await reader.ReadAsync())
        {
            var builder = new StringBuilder();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                builder.Append(i > 0 ? " | " : string.Empty).Append(reader.GetValue(i));
            }

            TestOutput.WriteLine(builder.ToString());
        }

        TestOutput.WriteLine(string.Empty);
    }
}
