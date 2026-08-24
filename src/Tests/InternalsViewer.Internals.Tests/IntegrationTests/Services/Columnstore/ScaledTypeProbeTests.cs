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
/// Whether the value page flags nibble marks the decimal type or any value held as a scaled integer
/// </summary>
/// <remarks>
/// Every narrow value page in the lab is a decimal, so the flag and the reserved bit cannot be told apart there.
/// Money is scaled the same way and is a different type, which separates the two readings.
/// </remarks>
public sealed class ScaledTypeProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegScaled";

    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Scaled_Types()
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

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        foreach (var rowGroup in index.CompressedRowGroups.Take(1))
        {
            foreach (var segment in rowGroup.Segments)
            {
                var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId);

                if (column?.Structure is null)
                {
                    continue;
                }

                var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                if (blob.VariableLengthData is not { } store || store.Pages.Length == 0)
                {
                    _lines.Add($"{column.Name} [{column.Structure.DataType}] enc {(int)segment.Encoding} "
                               + $"rleType {(int)blob.Header.RleType} no value store");

                    continue;
                }

                var page = store.Pages[0];

                _lines.Add($"=== {column.Name} [{column.Structure.DataType} scale {column.Structure.Scale}] "
                           + $"enc {(int)segment.Encoding} flags {page.Flags} compression {page.Compression} "
                           + $"valueSize {page.ValueSize} ===");

                for (var slot = 0; slot < Math.Min(3, page.ValueCount); slot++)
                {
                    var raw = page.GetRawValue(slot);

                    _lines.Add($"  slot {slot} raw {raw} shifted {raw >> 1} lowBit {raw & 1}");
                }

                await using var command = new SqlCommand(
                    $"SELECT TOP (3) {column.Name} FROM {TableName} ORDER BY Id", connection);

                await using var rows = await command.ExecuteReaderAsync();

                var values = new List<string>();

                while (await rows.ReadAsync())
                {
                    values.Add(rows.GetValue(0).ToString() ?? string.Empty);
                }

                _lines.Add($"  table holds {string.Join(", ", values)}");
            }
        }

        ProbeDump.Write("scaled_type_probe.txt", _lines);

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

        var script = $"""
            CREATE TABLE {TableName} (Id int NOT NULL, Cash money NOT NULL, Small smallmoney NOT NULL,
                Dec18 decimal(18,4) NOT NULL, Stamp datetime2(7) NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{TableName} ON {TableName};
            INSERT INTO {TableName} WITH (TABLOCK)
            SELECT Id,
                   CAST(Id AS money) / 10000,
                   CAST(Id AS smallmoney) / 100,
                   CAST(Id AS decimal(18,4)) / 10000,
                   DATEADD(millisecond, Id, '2020-01-01')
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
