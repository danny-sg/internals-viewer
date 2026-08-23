using System.Text;
using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Reports how many rowsets a partitioned columnstore has, a delete bitmap belonging to a partition rather than an index
/// </summary>
public sealed class PartitionedColumnstoreProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegPartitioned";

    [RequiresConnectionStringFact("local")]
    public async Task Report_Rowsets_Per_Partition()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{TableName}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is null or DBNull)
            {
                await Build(connection);
            }
        }

        await Dump(connection,
                   """
                   SELECT p.partition_number, p.hobt_id, p.rows,
                          rg.row_group_id, rg.state_desc, rg.total_rows, rg.deleted_rows
                   FROM sys.partitions p
                   JOIN sys.tables t ON t.object_id = p.object_id
                   LEFT JOIN sys.dm_db_column_store_row_group_physical_stats rg
                          ON rg.object_id = p.object_id AND rg.partition_number = p.partition_number
                   WHERE t.name = 'SegPartitioned'
                   ORDER BY p.partition_number, rg.row_group_id
                   """);

        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        foreach (var unit in database.AllocationUnits.Values
                                     .Where(a => a.TableName == TableName)
                                     .GroupBy(a => a.PartitionId)
                                     .Select(g => g.First()))
        {
            var index = await service.GetIndex(unit, database, CancellationToken.None);

            var bitmaps = index.Rowsets.Count(r => r.RowsetType
                                                   == InternalsViewer.Internals.Columnstore.Metadata.Enums
                                                                     .ColumnstoreRowsetType.DeleteBitmap);

            TestOutput.WriteLine($"unit hobt {unit.PartitionId} part {unit.PartitionNumber} "
                                 + $"owner {unit.OwnerType}: rowsets {index.Rowsets.Count}, "
                                 + $"delete bitmaps {bitmaps}, row groups {index.RowGroups.Count}, "
                                 + $"picked bitmap hobt {index.DeleteBitmap?.HobtId}");

            // Each partition keeps its own delete bitmap, so an index must only ever see the one belonging to it
            Assert.Equal(1, bitmaps);

            Assert.All(index.Rowsets, r => Assert.Equal(unit.PartitionNumber,
                                                        r.AllocationUnits[0].PartitionNumber));
        }

        await Dump(connection,
                   """
                   SELECT p.partition_number, s.column_id, s.segment_id AS RowGroup, s.encoding_type,
                          d.dictionary_id, d.entry_count, d.type AS DictType
                   FROM sys.column_store_segments s
                   JOIN sys.partitions p ON p.partition_id = s.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   LEFT JOIN sys.column_store_dictionaries d ON d.hobt_id = s.hobt_id
                                                            AND d.column_id = s.column_id
                   WHERE t.name = 'SegPartitioned'
                   ORDER BY p.partition_number, s.column_id
                   """);

        await Dump(connection,
                   """
                   SELECT it.internal_type_desc, COUNT(*) AS Rowsets,
                          COUNT(DISTINCT p.partition_number) AS Partitions
                   FROM sys.internal_tables it
                   JOIN sys.tables t ON t.object_id = it.parent_object_id
                   JOIN sys.partitions p ON p.object_id = it.object_id
                   WHERE t.name = 'SegPartitioned'
                   GROUP BY it.internal_type_desc
                   """);
    }

    private static async Task Build(SqlConnection connection)
    {
        await Execute(connection,
                      "CREATE PARTITION FUNCTION PF_SegPartitioned (int) AS RANGE LEFT FOR VALUES (1000, 2000)");

        await Execute(connection,
                      "CREATE PARTITION SCHEME PS_SegPartitioned AS PARTITION PF_SegPartitioned ALL TO ([PRIMARY])");

        await Execute(connection,
                      $"CREATE TABLE {TableName} (Id int NOT NULL, Note varchar(20) NOT NULL) ON PS_SegPartitioned (Id)");

        await Execute(connection,
                      $"""
                       INSERT INTO {TableName} (Id, Note)
                       SELECT TOP (3000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), 'row'
                       FROM sys.all_columns a CROSS JOIN sys.all_columns b
                       """);

        await Execute(connection,
                      $"CREATE CLUSTERED COLUMNSTORE INDEX CCI_{TableName} ON {TableName} ON PS_SegPartitioned (Id)");

        // One delete in each of the three partitions
        await Execute(connection, $"DELETE FROM {TableName} WHERE Id IN (5, 1500, 2500)");

        await Execute(connection, "CHECKPOINT");
    }

    private async Task Dump(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };

        await using var reader = await command.ExecuteReaderAsync();

        TestOutput.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)));

        while (await reader.ReadAsync())
        {
            var builder = new StringBuilder();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                builder.Append(i > 0 ? " | " : string.Empty).Append(reader.IsDBNull(i) ? "(null)" : reader.GetValue(i));
            }

            TestOutput.WriteLine(builder.ToString());
        }

        TestOutput.WriteLine(string.Empty);
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };

        await command.ExecuteNonQueryAsync();
    }
}
