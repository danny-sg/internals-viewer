using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class FloatDictionaryProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegFloatDict";

    private const int Rows = 1048576;

    private const int SmallSet = 2000;

    private const int WideSet = 12000;

    [RequiresConnectionStringFact("local")]
    public async Task Build_And_Report_Float_Dictionary()
    {
        await BuildTable();

        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == TableName);

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        var lines = new List<string>();

        foreach (var rowGroup in index.CompressedRowGroups)
        {
            foreach (var segment in rowGroup.Segments)
            {
                var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId);

                var dictionary = segment.LocalDictionary ?? column?.GlobalDictionary;

                var blobDescription = "none";

                if (dictionary is not null)
                {
                    try
                    {
                        var blob = await service.GetDictionaryBlob(database, dictionary, CancellationToken.None);

                        blobDescription = $"{blob.GetType().Name} lobtype {(int)blob.LobType} entries {blob.EntryCount}";

                        if (blob is NumericDictionary numeric)
                        {
                            blobDescription += $" | onDisk {dictionary.OnDiskSize} expected "
                                               + $"{56 + (numeric.ValueCount * numeric.ElementSize)}"
                                               + $" | hash buckets {numeric.HashTable.BucketCount} "
                                               + $"size {numeric.HashTable.BucketSize} "
                                               + $"entries {numeric.HashTable.EntryCount} "
                                               + $"entrySize {numeric.HashTable.EntrySize} "
                                               + $"maxLocal {numeric.HashTable.MaxLocalEntryCount} "
                                               + $"collisions {numeric.HashTable.CollisionCount} "
                                               + $"mask {numeric.HashTable.BucketIndexMask:x8} "
                                               + $"subLob {(int)numeric.HashTable.SubLobType}";
                        }
                    }
                    catch (Exception exception)
                    {
                        blobDescription = exception.Message;
                    }
                }

                lines.Add($"rg {rowGroup.RowGroupId} col {segment.Key.ColumnId} {column?.Name} "
                          + $"{column?.Structure?.DataType} | encoding {(int)segment.Encoding} "
                          + $"| pri {segment.PrimaryDictionaryId} sec {segment.SecondaryDictionaryId} "
                          + $"| dict type {dictionary?.Type} id {dictionary?.DictionaryId} "
                          + $"lastId {dictionary?.LastId} entries {dictionary?.EntryCount} | {blobDescription}");
            }
        }

        await using (var verify = new SqlConnection(ConnectionStringHelper.GetConnectionString("local")))
        {
            await verify.OpenAsync();

            foreach (var (columnId, columnName) in new[] { (3, "SmallFloat"), (4, "WideFloat"), (5, "SmallReal") })
            {
                var segment = index.CompressedRowGroups
                                   .First()
                                   .Segments
                                   .First(s => s.Key.ColumnId == columnId);

                var segmentReader = await service.GetSegmentReader(database, segment, CancellationToken.None);

                var matched = 0;

                var checkedRows = 0;

                for (var i = 0; i < Math.Min(20, segment.RowCount); i++)
                {
                    var value = segmentReader.GetValue(i);

                    if (i < 3)
                    {
                        lines.Add($"  {columnName}[{i}] dataId {segmentReader.DataIds.GetRowDataId(i)} "
                                  + $"=> {value} ({value?.GetType().Name})");
                    }

                    var sql = $"SELECT COUNT(*) FROM {TableName} WHERE {columnName} = @value";

                    await using var command = new SqlCommand(sql, verify);

                    command.Parameters.AddWithValue("@value", value ?? DBNull.Value);

                    checkedRows++;

                    if (await command.ExecuteScalarAsync() is int count && count > 0)
                    {
                        matched++;
                    }
                }

                lines.Add($"  {columnName}: {matched}/{checkedRows} decoded values found in the table");
            }
        }

        foreach (var line in lines)
        {
            TestOutput.WriteLine(line);
        }

        ProbeDump.Write("float_dict_probe.txt", lines);
    }

    private async Task BuildTable()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{TableName}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        await Execute(connection, $"DROP TABLE IF EXISTS {TableName}Stage");

        await Execute(connection, $"DROP TABLE IF EXISTS {TableName}Values");

        await Execute(connection,
                      $"""
                       CREATE TABLE {TableName}Values
                       (
                           Ordinal int NOT NULL PRIMARY KEY,
                           FloatValue float NOT NULL,
                           RealValue real NOT NULL
                       )
                       """);

        await Execute(connection,
                      $"""
                       INSERT INTO {TableName}Values (Ordinal, FloatValue, RealValue)
                       SELECT TOP ({WideSet}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1,
                              RAND(CHECKSUM(NEWID())) * POWER(CAST(10 AS float), (ABS(CHECKSUM(NEWID())) % 30) - 15),
                              CAST(RAND(CHECKSUM(NEWID()))
                                   * POWER(CAST(10 AS float), (ABS(CHECKSUM(NEWID())) % 20) - 10) AS real)
                       FROM sys.all_columns a CROSS JOIN sys.all_columns b
                       """,
                      timeoutSeconds: 300);

        await Execute(connection,
                      $"""
                       CREATE TABLE {TableName}Stage
                       (
                           Id int NOT NULL,
                           SmallOrdinal int NOT NULL,
                           WideOrdinal int NOT NULL,
                           RealOrdinal int NOT NULL
                       )
                       """);

        await Execute(connection,
                      $"""
                       INSERT INTO {TableName}Stage (Id, SmallOrdinal, WideOrdinal, RealOrdinal)
                       SELECT TOP ({Rows})
                              CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                              ABS(CHECKSUM(NEWID())) % {SmallSet},
                              ABS(CHECKSUM(NEWID())) % {WideSet},
                              ABS(CHECKSUM(NEWID())) % {SmallSet}
                       FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c
                       """,
                      timeoutSeconds: 600);

        await Execute(connection,
                      $"""
                       CREATE TABLE {TableName}
                       (
                           Id int NOT NULL,
                           SmallFloat float NOT NULL,
                           WideFloat float NOT NULL,
                           SmallReal real NOT NULL
                       )
                       """);

        await Execute(connection, $"CREATE CLUSTERED COLUMNSTORE INDEX CCI_{TableName} ON {TableName}");

        await Execute(connection,
                      $"""
                       INSERT INTO {TableName} WITH (TABLOCK) (Id, SmallFloat, WideFloat, SmallReal)
                       SELECT g.Id, s.FloatValue, w.FloatValue, r.RealValue
                       FROM {TableName}Stage g
                       JOIN {TableName}Values s ON s.Ordinal = g.SmallOrdinal
                       JOIN {TableName}Values w ON w.Ordinal = g.WideOrdinal
                       JOIN {TableName}Values r ON r.Ordinal = g.RealOrdinal
                       OPTION (MAXDOP 1)
                       """,
                      timeoutSeconds: 900);

        await Execute(connection, $"DROP TABLE {TableName}Stage");

        await Execute(connection, $"DROP TABLE {TableName}Values");

        await Execute(connection, "CHECKPOINT");
    }

    private static async Task Execute(SqlConnection connection, string sql, int timeoutSeconds = 60)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = timeoutSeconds };

        await command.ExecuteNonQueryAsync();
    }
}
