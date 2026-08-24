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
/// Whether the reserved low bit is taken off every stored value or only the ones the flags nibble marks
/// </summary>
public sealed class ReservedBitProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Reserved_Bit_Against_Flags()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        foreach (var allocationUnit in database.AllocationUnits.Values.Where(a => a.TableName.StartsWith("Seg")))
        {
            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            foreach (var rowGroup in index.CompressedRowGroups.Take(1))
            {
                foreach (var segment in rowGroup.Segments)
                {
                    var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId);

                    if (column?.Structure is null)
                    {
                        continue;
                    }

                    InternalsViewer.Internals.Columnstore.Segments.SegmentBlob blob;

                    try
                    {
                        blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);
                    }
                    catch
                    {
                        continue;
                    }

                    if (blob.VariableLengthData is not { } store || store.Pages.Length == 0)
                    {
                        continue;
                    }

                    var page = store.Pages[0];

                    // A narrow value is the only one the reserved bit shift is applied to
                    if (page.ValueSize is <= 0 or > 8)
                    {
                        continue;
                    }

                    _lines.Add($"=== {allocationUnit.TableName}.{column.Name} [{column.Structure.DataType} "
                               + $"scale {column.Structure.Scale}] flags {page.Flags} valueSize {page.ValueSize} ===");

                    for (var slot = 0; slot < Math.Min(3, page.ValueCount); slot++)
                    {
                        var raw = page.GetRawValue(slot);

                        _lines.Add($"  slot {slot} raw {raw} shifted {raw >> 1} lowBit {raw & 1}");
                    }

                    try
                    {
                        await using var command = new SqlCommand(
                            $"SELECT TOP (3) {column.Name} FROM {allocationUnit.TableName} ORDER BY (SELECT NULL)",
                            connection);

                        await using var rows = await command.ExecuteReaderAsync();

                        var values = new List<string>();

                        while (await rows.ReadAsync())
                        {
                            values.Add(rows.GetValue(0).ToString() ?? string.Empty);
                        }

                        _lines.Add($"  table holds {string.Join(", ", values)}");
                    }
                    catch (Exception exception)
                    {
                        _lines.Add($"  table: {exception.Message}");
                    }
                }
            }
        }

        ProbeDump.Write("reserved_bit_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
