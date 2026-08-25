using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// A segment wide enough to need sixteen byte RLE entries AND holding a repeat run, which nothing else does
/// </summary>
/// <remarks>
/// Every wide entry seen so far is a read run or a terminator, so the four bytes past the count read 01 on one and
/// 00 on the other with no way to tell a live entry flag from a run kind. A repeat run separates the two.
/// </remarks>
public sealed class WideRepeatRunProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegWideRepeat";

    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_A_Wide_Repeat_Run()
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

        foreach (var rowGroup in index.CompressedRowGroups)
        {
            var segment = rowGroup.Segments.First(s => s.Key.ColumnId == 3);

            var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

            _lines.Add($"rg{rowGroup.RowGroupId} enc {(int)segment.Encoding} rleType {(int)blob.Header.RleType} "
                       + $"entryBytes {blob.Header.RleEntrySize} entries {blob.Header.RleEntryCount} "
                       + $"base {segment.BaseId} magnitude {segment.Magnitude} "
                       + $"min {segment.MinDataId} max {segment.MaxDataId} rows {segment.RowCount}");

            if (blob.Header.RleEntrySize <= SegmentBlob.EntrySize)
            {
                _lines.Add("  entries are narrow, the value range did not force the wide layout");

                continue;
            }

            var span = blob.Data.Span;

            for (var i = 0; i < blob.Header.RleEntryCount; i++)
            {
                var entry = blob.RleEntries[i];

                var at = blob.Header.RleArrayOffset + (i * blob.Header.RleEntrySize) + 12;

                var tail = at + 4 <= span.Length
                    ? $"{span[at]:X2} {span[at + 1]:X2} {span[at + 2]:X2} {span[at + 3]:X2}"
                    : "past end";

                _lines.Add($"  [{i}] {(entry.IsTerminator ? "terminator" : entry.IsValue ? "REPEAT" : "read")} "
                           + $"value 0x{(ulong)entry.Value:X16} count {entry.Count} bytes12to15 [{tail}]");
            }
        }

        ProbeDump.Write("wide_repeat_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
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

        // The span has to exceed a signed integer whatever base id is chosen, so the values run the full range
        var script = $"""
            CREATE TABLE {TableName} (Id int NOT NULL, Big bigint NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{TableName} ON {TableName};
            INSERT INTO {TableName} WITH (TABLOCK)
            SELECT Id,
                   CASE WHEN Id % 10 = 0 THEN CAST(4100000000 AS bigint)
                        ELSE CAST(ABS(CHECKSUM(NEWID())) AS bigint) * 2 END
            FROM (SELECT TOP (200000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS Id
                  FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c) AS Source;
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 1800 };

            await command.ExecuteNonQueryAsync();
        }

        await using var compress = new SqlCommand(
            $"ALTER INDEX ALL ON {TableName} REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON)", connection)
        {
            CommandTimeout = 1800
        };

        await compress.ExecuteNonQueryAsync();
    }
}
