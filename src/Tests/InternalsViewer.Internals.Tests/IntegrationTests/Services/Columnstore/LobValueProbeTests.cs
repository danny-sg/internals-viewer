using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Whether a dictionary value held behind a LOB pointer now reads back as the row's own value
/// </summary>
public sealed class LobValueProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegLongString";

    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Lob_Backed_Values()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == TableName);

        if (allocationUnit is null)
        {
            _lines.Add($"{TableName} not found");

            Write();

            return;
        }

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        foreach (var rowGroup in index.CompressedRowGroups)
        {
            foreach (var segment in rowGroup.Segments.Where(s => s.Key.ColumnId > 2))
            {
                var dictionaryMetadata = segment.LocalDictionary
                    ?? index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId)?.GlobalDictionary;

                if (dictionaryMetadata is null)
                {
                    continue;
                }

                var blob = await service.GetDictionaryBlob(database, dictionaryMetadata, CancellationToken.None);

                if (blob is not StringDictionary dictionary)
                {
                    continue;
                }

                var pointers = Enumerable.Range(0, dictionary.Handles.Length)
                                         .Count(i => dictionary.TryGetLobPointer(i, out _));

                _lines.Add($"col{segment.Key.ColumnId} entries {dictionary.Handles.Length} "
                           + $"lobPointers {pointers} resolved {dictionary.LobValues.Count}");

                if (pointers == 0)
                {
                    continue;
                }

                var reader2 = await service.GetSegmentReader(database, segment, CancellationToken.None);

                var idReader = await service.GetSegmentReader(database,
                                                              rowGroup.Segments.First(s => s.Key.ColumnId == 2),
                                                              CancellationToken.None);

                var matched = 0;

                var checkedRows = 0;

                var mismatch = string.Empty;

                var columnName = index.Columns.First(c => c.ColumnStoreColumnId == segment.Key.ColumnId).Name;

                foreach (var row in new[] { 0, 1, 2, segment.RowCount / 2, segment.RowCount - 1 })
                {
                    if (row < 0 || row >= segment.RowCount)
                    {
                        continue;
                    }

                    var decoded = reader2.GetValue(row);

                    var text = decoded is byte[] bytes ? System.Text.Encoding.Latin1.GetString(bytes) : decoded?.ToString();

                    await using var command = new SqlCommand(
                        $"SELECT DATALENGTH({columnName}), LEFT(CAST({columnName} AS varchar(max)), 20) "
                        + $"FROM {TableName} WHERE Id = @id", connection);

                    command.Parameters.AddWithValue("@id", idReader.GetValue(row) ?? (object)DBNull.Value);

                    await using var rows = await command.ExecuteReaderAsync();

                    while (await rows.ReadAsync())
                    {
                        checkedRows++;

                        var actualLength = rows.IsDBNull(0) ? 0 : Convert.ToInt32(rows.GetValue(0));

                        var actualStart = rows.IsDBNull(1) ? string.Empty : rows.GetString(1);

                        if (text is not null && text.Length == actualLength && text.StartsWith(actualStart))
                        {
                            matched++;
                        }
                        else if (mismatch.Length == 0)
                        {
                            mismatch = $" MISMATCH row {row} decodedLength {text?.Length ?? -1} "
                                       + $"actualLength {actualLength} decodedStart "
                                       + $"'{(text is null ? string.Empty : text[..Math.Min(20, text.Length)])}' "
                                       + $"actualStart '{actualStart}'";
                        }
                    }
                }

                _lines.Add($"  MATCHED {matched}/{checkedRows}{mismatch}");
            }
        }

        Write();
    }

    private void Write()
    {
        ProbeDump.Write("lob_value_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
