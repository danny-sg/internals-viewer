using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class VariableWidthStringProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Variable_Width_Strings()
    {
        await BuildTable();

        var database = await LoadDatabase();

        var service = CreateService();

        await Report(service, database, "SegHugeDict", 3, "Code");

        await Report(service, database, "SegVarString", 3, "Text1");

        await Report(service, database, "SegVarString", 4, "TextNull");

        ProbeDump.Write("varstring_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private async Task Report(ColumnstoreService service,
                              InternalsViewer.Internals.Engine.Database.DatabaseSource database,
                              string tableName,
                              int columnId,
                              string columnName)
    {
        var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == tableName);

        if (allocationUnit is null)
        {
            _lines.Add($"=== {tableName}: not found ===");

            return;
        }

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        var rowGroup = index.CompressedRowGroups.FirstOrDefault();

        if (rowGroup is null)
        {
            _lines.Add($"=== {tableName}: no compressed row groups ===");

            return;
        }

        var segment = rowGroup.Segments.FirstOrDefault(s => s.Key.ColumnId == columnId);

        if (segment is null)
        {
            _lines.Add($"=== {tableName} col{columnId}: not found ===");

            return;
        }

        _lines.Add($"=== {tableName}.{columnName} enc {(int)segment.Encoding} rows {segment.RowCount} ===");

        var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

        if (blob.VariableLengthData is not { } store)
        {
            _lines.Add($"  no value store, structure {(int)blob.Header.RleType}");

            return;
        }

        _lines.Add($"  store valueCount {store.ValueCount} pages {store.PageCount} "
                   + $"| valueSizes [{string.Join(",", store.Pages.Select(p => p.ValueSize).Distinct())}] "
                   + $"compression [{string.Join(",", store.Pages.Select(p => p.Compression).Distinct())}]");

        var idSegment = rowGroup.Segments.First(s => s.Key.ColumnId == 2);

        var idReader = await service.GetSegmentReader(database, idSegment, CancellationToken.None);

        var reader = await service.GetSegmentReader(database, segment, CancellationToken.None);

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        var matched = 0;

        var checkedRows = 0;

        var firstMismatch = string.Empty;

        for (var i = 0; i < Math.Min(20, store.ValueCount); i++)
        {
            var decoded = reader.GetValue(i)?.ToString();

            await using var command = new SqlCommand(
                $"SELECT {columnName} FROM {tableName} WHERE Id = @id", connection);

            command.Parameters.AddWithValue("@id", idReader.GetValue(i) ?? (object)DBNull.Value);

            var actual = await command.ExecuteScalarAsync();

            var expected = actual is DBNull or null ? null : actual.ToString();

            checkedRows++;

            if (decoded == expected)
            {
                matched++;
            }
            else if (firstMismatch.Length == 0)
            {
                firstMismatch = $" FIRST MISMATCH row {i} decoded '{decoded ?? "<null>"}' actual '{expected ?? "<null>"}'";
            }

            if (i < 4)
            {
                _lines.Add($"    row {i} decoded '{decoded ?? "<null>"}' actual '{expected ?? "<null>"}'");
            }
        }

        _lines.Add($"  MATCHED {matched}/{checkedRows}{firstMismatch}");
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

    private async Task BuildTable()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand("SELECT OBJECT_ID('SegVarString')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        // High cardinality strings of varying length, which is what a variable width page is actually for
        var script = """
            CREATE TABLE SegVarString (Id int NOT NULL, Text1 nvarchar(40) NOT NULL, TextNull nvarchar(40) NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegVarString ON SegVarString;
            INSERT INTO SegVarString WITH (TABLOCK)
            SELECT TOP (1048576) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   REPLICATE(N'v', 1 + ABS(CHECKSUM(NEWID())) % 30) + CAST(ABS(CHECKSUM(NEWID())) AS nvarchar(10)),
                   CASE WHEN ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 3 = 0 THEN NULL
                        ELSE REPLICATE(N'n', 1 + ABS(CHECKSUM(NEWID())) % 20)
                             + CAST(ABS(CHECKSUM(NEWID())) AS nvarchar(10)) END
            FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c;
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 900 };

            await command.ExecuteNonQueryAsync();
        }
    }
}
