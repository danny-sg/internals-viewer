using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class OrderedColumnstoreProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Ordered_Columnstore()
    {
        await using (var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local")))
        {
            await connection.OpenAsync();

            _lines.Add($"version {await Scalar(connection, "SELECT @@VERSION")}");

            _lines.Add($"compatibility {await Scalar(connection, "SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID()")}");

            await BuildTables(connection);

            await ReportCatalog(connection, "SegOrdered");

            await ReportCatalog(connection, "SegUnordered");
        }

        var database = await LoadDatabase();

        var service = CreateService();

        await ReportBlob(service, database, "SegOrdered");

        await ReportBlob(service, database, "SegUnordered");

        ProbeDump.Write("ordered_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    /// <summary>
    /// What the catalog says about the ordering, and whether the row groups came out disjoint
    /// </summary>
    private async Task ReportCatalog(SqlConnection connection, string tableName)
    {
        _lines.Add($"=== {tableName} catalog ===");

        await using (var command = new SqlCommand($"""
                                                   SELECT c.name, ic.column_store_order_ordinal, ic.key_ordinal, i.type_desc
                                                   FROM sys.indexes i
                                                   JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                                                   JOIN sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
                                                   WHERE i.object_id = OBJECT_ID('{tableName}')
                                                   """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _lines.Add($"  index column {reader.GetValue(0)} orderOrdinal {reader.GetValue(1)} "
                           + $"keyOrdinal {reader.GetValue(2)} type {reader.GetValue(3)}");
            }
        }

        await using (var command = new SqlCommand($"""
                                                   SELECT rg.row_group_id, rg.total_rows, rg.size_in_bytes,
                                                          s.min_data_id, s.max_data_id, s.column_id
                                                   FROM sys.column_store_row_groups rg
                                                   JOIN sys.column_store_segments s ON s.hobt_id =
                                                        (SELECT hobt_id FROM sys.partitions WHERE object_id = rg.object_id AND index_id = rg.index_id)
                                                        AND s.segment_id = rg.row_group_id
                                                   WHERE rg.object_id = OBJECT_ID('{tableName}') AND s.column_id = 1
                                                   ORDER BY rg.row_group_id
                                                   """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _lines.Add($"  rowGroup {reader.GetValue(0)} rows {reader.GetValue(1)} bytes {reader.GetValue(2)} "
                           + $"col1 minId {reader.GetValue(3)} maxId {reader.GetValue(4)}");
            }
        }

    }

    private async Task ReportBlob(ColumnstoreService service, DatabaseSource database, string tableName)
    {
        var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == tableName);

        if (allocationUnit is null)
        {
            _lines.Add($"=== {tableName}: not found ===");

            return;
        }

        _lines.Add($"=== {tableName} blob ===");

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        var indexColumns = database.Metadata.IndexColumns
                                   .SelectMany(g => g)
                                   .Where(c => c.ObjectId == allocationUnit.ObjectId && c.IndexId == allocationUnit.IndexId)
                                   .OrderBy(c => c.IndexColumnId);

        foreach (var indexColumn in indexColumns)
        {
            _lines.Add($"  sysiscols subid {indexColumn.IndexColumnId} columnId {indexColumn.ColumnId} "
                       + $"keyOrdinal {indexColumn.KeyOrdinal} partitionOrdinal {indexColumn.PartitionOrdinal} "
                       + $"tinyprop3 {indexColumn.TinyProp3} orderOrdinal {indexColumn.ColumnStoreOrderOrdinal} "
                       + $"status {indexColumn.Status}");
        }

        foreach (var column in index.Columns)
        {
            _lines.Add($"  model column {column.ColumnStoreColumnId} {column.Name} "
                       + $"orderOrdinal {column.OrderOrdinal} isOrdered {column.IsOrdered}");
        }

        foreach (var rowGroup in index.CompressedRowGroups.Take(3))
        {
            _lines.Add($"  rowGroup {rowGroup.RowGroupId} rows {rowGroup.TotalRows} state {rowGroup.State} "
                       + $"metadataBlob {rowGroup.MetadataBlob} generation {rowGroup.Generation} flags {rowGroup.Flags}");

            foreach (var segment in rowGroup.Segments.Where(s => s.Key.ColumnId <= 3))
            {
                var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId);

                try
                {
                    var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                    _lines.Add($"    col{segment.Key.ColumnId} {column?.Name} enc {(int)segment.Encoding} "
                               + $"structure {(int)blob.Header.RleType} unknown0C 0x{blob.Header.Unknown0C:X} "
                               + $"rle {blob.Header.RleEntryCount} bitpack {blob.Header.BitpackUnitCount} "
                               + $"bookmarks {blob.Header.BookmarkCount} minId {segment.MinDataId} "
                               + $"maxId {segment.MaxDataId} bloom {segment.BloomFilterMetadata:X} "
                               + $"size {segment.OnDiskSize}");
                }
                catch (Exception exception)
                {
                    _lines.Add($"    col{segment.Key.ColumnId} {column?.Name}: {exception.Message}");
                }
            }
        }
    }

    private static async Task<object?> Scalar(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);

        return await command.ExecuteScalarAsync();
    }

    private ColumnstoreService CreateService()
    {
        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        return new ColumnstoreService(reader, new LobDataService(pageService));
    }

    private async Task BuildTables(SqlConnection connection)
    {
        // Ordering is what a scan uses to skip whole row groups, so the values are shuffled before loading
        await Build(connection, "SegOrdered", """
            CREATE TABLE SegOrdered (Id int NOT NULL, Spread int NOT NULL, Filler varchar(50) NOT NULL);
            INSERT INTO SegOrdered WITH (TABLOCK)
            SELECT TOP (2200000) CAST(ABS(CHECKSUM(NEWID())) % 2000000 AS int),
                   CAST(ABS(CHECKSUM(NEWID())) % 1000 AS int),
                   'filler'
            FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c;
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegOrdered ON SegOrdered ORDER (Id);
            """);

        await Build(connection, "SegUnordered", """
            CREATE TABLE SegUnordered (Id int NOT NULL, Spread int NOT NULL, Filler varchar(50) NOT NULL);
            INSERT INTO SegUnordered WITH (TABLOCK)
            SELECT TOP (2200000) CAST(ABS(CHECKSUM(NEWID())) % 2000000 AS int),
                   CAST(ABS(CHECKSUM(NEWID())) % 1000 AS int),
                   'filler'
            FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c;
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegUnordered ON SegUnordered;
            """);
    }

    private static async Task Build(SqlConnection connection, string tableName, string script)
    {
        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{tableName}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 1800 };

            await command.ExecuteNonQueryAsync();
        }
    }
}
