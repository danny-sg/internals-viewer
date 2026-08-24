using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class UnseenStructureProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private static readonly int[] KnownSubLobTypes = [0, 1, 2, 4, 5, 6, 8, 9];

    private static readonly int[] KnownStructureTypes = [3, 7];

    private static readonly int[] KnownDictionaryTypes = [1, 3, 4];

    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_For_Unseen_Structures()
    {
        await BuildTables();

        var database = await LoadDatabase();

        var service = CreateService();

        var tables = new[] { "SegLongString", "SegArchiveBig", "SegHugeDict", "SegLongStringArchive" };

        foreach (var tableName in tables)
        {
            await Report(service, database, tableName);
        }

        ProbeDump.Write("unseen_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private async Task Report(ColumnstoreService service, DatabaseSource database, string tableName)
    {
        var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == tableName);

        if (allocationUnit is null)
        {
            _lines.Add($"=== {tableName}: not found ===");

            return;
        }

        _lines.Add($"=== {tableName} ===");

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        foreach (var rowGroup in index.CompressedRowGroups)
        {
            foreach (var segment in rowGroup.Segments)
            {
                await ReportSegment(service, database, index, rowGroup, segment);
            }
        }
    }

    private async Task ReportSegment(ColumnstoreService service,
                                     DatabaseSource database,
                                     ColumnStoreIndex index,
                                     RowGroup rowGroup,
                                     ColumnSegment segment)
    {
        var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId);

        var prefix = $"rg{rowGroup.RowGroupId} col{segment.Key.ColumnId} {column?.Name}";

        try
        {
            var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

            var pageTypes = blob.VariableLengthData is { } store
                ? string.Join(",", store.Pages.Select(p => (int)p.SubLobType).Distinct())
                : "-";

            _lines.Add($"{prefix} enc {(int)segment.Encoding} structure {(int)blob.Header.StructureType} "
                       + $"unknown0C 0x{blob.Header.Unknown0C:X} rows {segment.RowCount} "
                       + $"size {segment.OnDiskSize} pageTypes {pageTypes} "
                       + $"bloom {segment.BloomFilterMetadata:X} bloomPointer {!segment.BloomFilterPointer.IsEmpty}");

            Flag($"{prefix} structure", (int)blob.Header.StructureType, KnownStructureTypes);

            if (blob.VariableLengthData is { } vld)
            {
                Flag($"{prefix} vld store subLob", (int)vld.Header.SubLobType, KnownSubLobTypes);

                foreach (var page in vld.Pages)
                {
                    Flag($"{prefix} vld page subLob", (int)page.SubLobType, KnownSubLobTypes);

                    if (page.Compression != 1 || page.Flags != 1)
                    {
                        _lines.Add($"  NEW: {prefix} vld page compression {page.Compression} flags {page.Flags}");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            _lines.Add($"{prefix}: {exception.Message}");
        }

        var metadata = segment.LocalDictionary ?? column?.GlobalDictionary;

        if (metadata is null)
        {
            return;
        }

        try
        {
            var dictionary = await service.GetDictionaryBlob(database, metadata, CancellationToken.None);

            var pages = dictionary is StringDictionary strings
                ? string.Join(",", strings.Pages.Select(p => (int)p.SubLobType).Distinct())
                : "-";

            var maxString = dictionary is StringDictionary s ? $"{s.Store.MaxStringSize}" : "-";

            _lines.Add($"  dict type {metadata.Type} lobtype {(int)dictionary.LobType} "
                       + $"entries {dictionary.EntryCount} size {metadata.OnDiskSize} "
                       + $"maxString {maxString} pageTypes {pages}");

            Flag($"  dict type", metadata.Type, KnownDictionaryTypes);

            if (dictionary is StringDictionary stringDictionary)
            {
                Flag("  dict store subLob", (int)stringDictionary.Store.SubLobType, KnownSubLobTypes);

                foreach (var page in stringDictionary.Pages)
                {
                    Flag("  dict page subLob", (int)page.SubLobType, KnownSubLobTypes);
                }
            }

            if (dictionary is NumericDictionary numeric && numeric.HashTable.IsPopulated)
            {
                _lines.Add($"  NEW: populated hash table buckets {numeric.HashTable.BucketCount} "
                           + $"entries {numeric.HashTable.EntryCount}");
            }
        }
        catch (Exception exception)
        {
            _lines.Add($"  dictionary: {exception.Message}");
        }
    }

    private void Flag(string label, int value, int[] known)
    {
        if (!known.Contains(value))
        {
            _lines.Add($"  NEW: {label} {value}");
        }
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

    private async Task BuildTables()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        // Strings past the 8192 byte MaxStringSize the string store declares, which nothing in the lab reaches
        await Build(connection, "SegLongString", """
            CREATE TABLE SegLongString (Id int NOT NULL, Repeated varchar(max) NOT NULL, Distinct1 varchar(max) NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegLongString ON SegLongString;
            INSERT INTO SegLongString WITH (TABLOCK) (Id, Repeated, Distinct1)
            SELECT TOP (2000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   REPLICATE(CAST('A' AS varchar(max)), 20000),
                   REPLICATE(CAST(CHAR(65 + (ABS(CHECKSUM(NEWID())) % 26)) AS varchar(max)), 20000)
                   + CAST(NEWID() AS varchar(40))
            FROM sys.all_columns a CROSS JOIN sys.all_columns b;
            """);

        await Build(connection, "SegLongStringArchive", """
            CREATE TABLE SegLongStringArchive (Id int NOT NULL, Big varchar(max) NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegLongStringArchive ON SegLongStringArchive
                WITH (DATA_COMPRESSION = COLUMNSTORE_ARCHIVE);
            INSERT INTO SegLongStringArchive WITH (TABLOCK) (Id, Big)
            SELECT TOP (2000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   REPLICATE(CAST(CHAR(65 + (ABS(CHECKSUM(NEWID())) % 26)) AS varchar(max)), 30000)
            FROM sys.all_columns a CROSS JOIN sys.all_columns b;
            """);

        // A full row group under archive compression, which is the largest single blob the lab can hold
        await Build(connection, "SegArchiveBig", """
            CREATE TABLE SegArchiveBig (Id bigint NOT NULL, Spread bigint NOT NULL, Text1 varchar(200) NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegArchiveBig ON SegArchiveBig
                WITH (DATA_COMPRESSION = COLUMNSTORE_ARCHIVE);
            INSERT INTO SegArchiveBig WITH (TABLOCK) (Id, Spread, Text1)
            SELECT TOP (1048576) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS bigint),
                   CAST(ABS(CHECKSUM(NEWID())) AS bigint) * 1000003,
                   REPLICATE(CAST(NEWID() AS varchar(40)), 5)
            FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c;
            """);

        // Far more distinct values than any dictionary in the lab, to see whether the buckets ever get written
        await Build(connection, "SegHugeDict", """
            CREATE TABLE SegHugeDict (Id int NOT NULL, Code varchar(30) NOT NULL, Number bigint NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegHugeDict ON SegHugeDict;
            INSERT INTO SegHugeDict WITH (TABLOCK) (Id, Code, Number)
            SELECT TOP (1048576) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   'C' + CAST(ABS(CHECKSUM(NEWID())) % 400000 AS varchar(20)),
                   CAST(ABS(CHECKSUM(NEWID())) % 400000 AS bigint) * 7919
            FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c;
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
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 900 };

            await command.ExecuteNonQueryAsync();
        }
    }
}
