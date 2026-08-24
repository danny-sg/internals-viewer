using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Every VLD page in the lab against the things its flags nibble might be tracking
/// </summary>
public sealed class ValuePageFlagsProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Value_Page_Flags()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var rows = new List<string>();

        var byFlag = new SortedDictionary<string, int>();

        foreach (var allocationUnit in database.AllocationUnits.Values.Where(a => a.TableName.StartsWith("Seg")))
        {
            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            foreach (var rowGroup in index.CompressedRowGroups)
            {
                foreach (var segment in rowGroup.Segments)
                {
                    try
                    {
                        var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                        if (blob.VariableLengthData is not { } store)
                        {
                            continue;
                        }

                        for (var i = 0; i < store.Pages.Length; i++)
                        {
                            var page = store.Pages[i];

                            var isLast = i == store.Pages.Length - 1;

                            var nulls = 0;

                            if (page.IsVariableWidth)
                            {
                                for (var slot = 0; slot < page.ValueCount; slot++)
                                {
                                    if (page.IsNull(slot))
                                    {
                                        nulls++;
                                    }
                                }
                            }

                            var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId);

                            Count(byFlag, $"{column?.Structure?.DataType,-16} scale {column?.Structure?.Scale,-3} "
                                          + $"flags {page.Flags}");

                            rows.Add($"{allocationUnit.TableName}.{column?.Name} [{column?.Structure?.DataType}] "
                                     + $"page {i}/{store.Pages.Length - 1} "
                                     + $"flags {page.Flags} compression {page.Compression} "
                                     + $"valueSize {page.ValueSize} values {page.ValueCount} nulls {nulls} "
                                     + $"size {page.Size} expanded {page.ExpandedSize} enc {(int)segment.Encoding}");
                        }
                    }
                    catch (Exception exception)
                    {
                        _lines.Add($"{allocationUnit.TableName} col{segment.Key.ColumnId}: {exception.Message}");
                    }
                }
            }
        }

        _lines.Add($"{rows.Count} value pages");

        foreach (var pair in byFlag)
        {
            _lines.Add($"  [{pair.Key}] x{pair.Value}");
        }

        _lines.Add(string.Empty);

        foreach (var row in rows.Where(r => r.Contains("flags 0 ")).Take(20))
        {
            _lines.Add($"FLAGS0 {row}");
        }

        foreach (var row in rows.Where(r => !r.Contains("flags 0 ")).Take(10))
        {
            _lines.Add($"OTHER  {row}");
        }

        ProbeDump.Write("value_page_flags_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private static void Count(SortedDictionary<string, int> into, string key)
    {
        into.TryGetValue(key, out var count);

        into[key] = count + 1;
    }
}
