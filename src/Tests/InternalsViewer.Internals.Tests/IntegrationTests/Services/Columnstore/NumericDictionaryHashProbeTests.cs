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

/// <summary>
/// Builds a columnstore table meant to force a populated hash table into a numeric dictionary, and reports what came out
/// </summary>
/// <remarks>
/// Every numeric dictionary in the lab tables declares a hash table and leaves it empty, and all of them are global.
/// This loads a column of high cardinality values a row group at a time, with the values of each disjoint from the
/// last, which is the shape that should stop one global dictionary covering the lot.
/// </remarks>
public sealed class NumericDictionaryHashProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegLocalDict";

    private const int RowGroups = 2;

    private const int RowsPerGroup = 1048576;

    private const int SmallSet = 2000;

    private const int MediumSet = 12000;

    private const int LargeSet = 50000;

    [RequiresConnectionStringFact("local")]
    public async Task Build_And_Report_Numeric_Dictionary_Hash()
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

        var rowGroups = index.CompressedRowGroups.ToList();

        var dictionaries = index.Columns
                                .Select(c => c.GlobalDictionary)
                                .Concat(rowGroups.SelectMany(r => r.Segments).Select(s => s.LocalDictionary))
                                .Where(d => d is not null)
                                .GroupBy(d => (d!.ColumnId, d.DictionaryId))
                                .Select(g => g.First())
                                .ToList();

        TestOutput.WriteLine($"{rowGroups.Count} compressed row groups, {dictionaries.Count} dictionaries");

        foreach (var metadata in dictionaries)
        {
            var blob = await service.GetDictionaryBlob(database, metadata!, CancellationToken.None);

            if (blob is not NumericDictionary numeric)
            {
                TestOutput.WriteLine($"  column {metadata!.ColumnId} dictionary {metadata.DictionaryId}: {blob.GetType().Name}");

                continue;
            }

            TestOutput.WriteLine($"  column {metadata!.ColumnId} dictionary {metadata.DictionaryId} "
                                 + $"{(metadata.IsGlobal ? "global" : "local")} entries {numeric.EntryCount} "
                                 + $"| buckets {numeric.BucketCount} size {numeric.BucketSize} "
                                 + $"| hash entries {numeric.HashEntryCount} size {numeric.HashEntrySize} "
                                 + $"| collisions {numeric.CollisionCount} mask {numeric.BucketIndexMask}");
        }
    }

    /// <summary>
    /// Loads columns drawn from value sets of a rising size, the values of each spread widely over the domain
    /// </summary>
    /// <remarks>
    /// Value hash encoding is what carries a numeric dictionary, and the engine only reaches for it when bit packing
    /// the values would cost more than holding them once and pointing at them. That needs the values spread widely,
    /// which random values drawn from a fixed set of them give and a running sequence does not. The sets here run to
    /// fifty thousand values, above the four thousand of the largest dictionary to hand.
    ///
    /// The pick of which value a row takes is landed in a staging table first. Drawing it in the join predicate
    /// instead lets the optimiser hoist the call out of the loop, leaving every row of the column the same value.
    ///
    /// The table is left in place once built, and dropping it is what asks for it to be built again.
    /// </remarks>
    private async Task BuildTable()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{TableName}')", connection))
        {
            // Rebuilding is a minute of DDL against a database the rest of the tests are reading, so it is done once
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
                           Value datetime NOT NULL
                       )
                       """);

        await Execute(connection,
                      $"""
                       INSERT INTO {TableName}Values (Ordinal, Value)
                       SELECT TOP ({LargeSet}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1,
                              DATEADD(millisecond,
                                      ABS(CHECKSUM(NEWID())) % 86400000,
                                      DATEADD(day, ABS(CHECKSUM(NEWID())) % 40000, '1900-01-01'))
                       FROM sys.all_columns a CROSS JOIN sys.all_columns b
                       """,
                      timeoutSeconds: 300);

        await Execute(connection,
                      $"""
                       CREATE TABLE {TableName}Stage
                       (
                           Id int NOT NULL,
                           SmallOrdinal int NOT NULL,
                           MediumOrdinal int NOT NULL,
                           LargeOrdinal int NOT NULL
                       )
                       """);

        await Execute(connection,
                      $"""
                       INSERT INTO {TableName}Stage (Id, SmallOrdinal, MediumOrdinal, LargeOrdinal)
                       SELECT TOP ({RowsPerGroup})
                              CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                              ABS(CHECKSUM(NEWID())) % {SmallSet},
                              ABS(CHECKSUM(NEWID())) % {MediumSet},
                              ABS(CHECKSUM(NEWID())) % {LargeSet}
                       FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c
                       """,
                      timeoutSeconds: 600);

        await Execute(connection,
                      $"""
                       CREATE TABLE {TableName}
                       (
                           Id int NOT NULL,
                           Small datetime NOT NULL,
                           Medium datetime NOT NULL,
                           Large datetime NOT NULL
                       )
                       """);

        await Execute(connection, $"CREATE CLUSTERED COLUMNSTORE INDEX CCI_{TableName} ON {TableName}");

        for (var group = 0; group < RowGroups; group++)
        {
            await Execute(connection,
                          $"""
                           INSERT INTO {TableName} WITH (TABLOCK) (Id, Small, Medium, Large)
                           SELECT g.Id, s.Value, m.Value, l.Value
                           FROM {TableName}Stage g
                           JOIN {TableName}Values s ON s.Ordinal = g.SmallOrdinal
                           JOIN {TableName}Values m ON m.Ordinal = g.MediumOrdinal
                           JOIN {TableName}Values l ON l.Ordinal = g.LargeOrdinal
                           OPTION (MAXDOP 1)
                           """,
                          timeoutSeconds: 900);
        }

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
