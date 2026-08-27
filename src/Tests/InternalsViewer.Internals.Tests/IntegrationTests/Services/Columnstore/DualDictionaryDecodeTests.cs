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
/// Decodes a segment whose ids span a global dictionary and the local one that carries on from it
/// </summary>
public sealed class DualDictionaryDecodeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegDualD";

    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Decodes_Across_Both_Dictionaries()
    {
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

        var matched = 0;

        var checkedRows = 0;

        var fromOverflow = 0;

        foreach (var rowGroup in index.CompressedRowGroups)
        {
            var segment = rowGroup.Segments.First(s => s.Key.ColumnId == 3);

            var idSegment = rowGroup.Segments.First(s => s.Key.ColumnId == 2);

            var noteReader = await service.GetSegmentReader(database, segment, CancellationToken.None);

            var idReader = await service.GetSegmentReader(database, idSegment, CancellationToken.None);

            var stream = noteReader.DataIds;

            var globalLastId = index.Columns.First(c => c.ColumnStoreColumnId == 3).GlobalDictionary is { } global
                ? global.LastId
                : 0;

            _lines.Add($"=== rg{rowGroup.RowGroupId} rows {segment.RowCount} globalLastId {globalLastId} "
                       + $"minId {segment.MinDataId} maxId {segment.MaxDataId} ===");

            // Sampled across the row group so ids either side of the boundary are covered
            var step = Math.Max(1, segment.RowCount / 40);

            for (var row = 0; row < segment.RowCount; row += step)
            {
                var dataId = stream.GetRowDataId(row);

                var decoded = noteReader.GetValue(row);

                var text = decoded is byte[] bytes ? System.Text.Encoding.Unicode.GetString(bytes) : decoded?.ToString();

                await using var command = new SqlCommand(
                    $"SELECT Note FROM {TableName} WHERE Id = @id", connection);

                command.Parameters.AddWithValue("@id", idReader.GetValue(row) ?? (object)DBNull.Value);

                var actual = (await command.ExecuteScalarAsync())?.ToString();

                checkedRows++;

                if (dataId > globalLastId)
                {
                    fromOverflow++;
                }

                if (text == actual)
                {
                    matched++;
                }
                else if (_lines.Count < 40)
                {
                    _lines.Add($"  MISMATCH row {row} dataId {dataId} decoded '{Trim(text)}' actual '{Trim(actual)}'");
                }
            }
        }

        _lines.Add($"MATCHED {matched}/{checkedRows}, {fromOverflow} of them from the overflow dictionary");

        ProbeDump.Write("dual_dictionary_decode.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }

        Assert.Equal(checkedRows, matched);

        Assert.True(fromOverflow > 0, "no sampled row read from the overflow dictionary");
    }

    private static string Trim(string? value)
        => value is null ? "<null>" : value.Length <= 24 ? value : value[..24] + "...";
}
