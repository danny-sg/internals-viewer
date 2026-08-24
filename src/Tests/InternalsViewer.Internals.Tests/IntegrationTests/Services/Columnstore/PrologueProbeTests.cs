using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// What sits in the two bytes a VLD segment carries between its header and its bookmark array
/// </summary>
public sealed class PrologueProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_The_Prologue_Tail()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var values = new SortedDictionary<string, int>();

        var bitPackTail = new SortedDictionary<string, int>();

        var examined = 0;

        foreach (var allocationUnit in database.AllocationUnits.Values.Where(a => a.TableName.StartsWith("Seg")))
        {
            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            foreach (var rowGroup in index.CompressedRowGroups)
            {
                foreach (var segment in rowGroup.Segments)
                {
                    if (examined >= 200)
                    {
                        break;
                    }

                    try
                    {
                        var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                        examined++;

                        var span = blob.Data.Span;

                        var tail = $"{span[0x30]:X2} {span[0x31]:X2}";

                        if (blob.Header.IsVariableLengthData)
                        {
                            values.TryGetValue(tail, out var count);

                            values[tail] = count + 1;
                        }
                        else
                        {
                            bitPackTail.TryGetValue(tail, out var count);

                            bitPackTail[tail] = count + 1;
                        }
                    }
                    catch (Exception exception)
                    {
                        _lines.Add($"{allocationUnit.TableName} col{segment.Key.ColumnId}: {exception.Message}");
                    }
                }
            }
        }

        _lines.Add($"VLD bytes at 0x30-0x31: {string.Join(", ", values.Select(v => $"[{v.Key}] x{v.Value}"))}");

        _lines.Add($"BIT PACK bytes at 0x30-0x31 (first bookmark): "
                   + $"{string.Join(", ", bitPackTail.Take(6).Select(v => $"[{v.Key}] x{v.Value}"))}");

        ProbeDump.Write("prologue_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
