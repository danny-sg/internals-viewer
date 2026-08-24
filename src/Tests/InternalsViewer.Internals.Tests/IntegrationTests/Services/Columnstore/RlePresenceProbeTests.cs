using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Whether an RLE array is always present and whether the entry count says when it carries runs
/// </summary>
public sealed class RlePresenceProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Rle_Presence()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var minimumArrayCount = int.MaxValue;

        var disagreements = 0;

        var withRuns = 0;

        var examined = 0;

        var shortestRun = int.MaxValue;

        foreach (var allocationUnit in database.AllocationUnits.Values.Where(a => a.TableName.StartsWith("Seg")))
        {
            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            foreach (var rowGroup in index.CompressedRowGroups)
            {
                foreach (var segment in rowGroup.Segments)
                {
                    if (examined >= 400)
                    {
                        break;
                    }

                    try
                    {
                        var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                        examined++;

                        minimumArrayCount = Math.Min(minimumArrayCount, blob.Header.RleArrayCount);

                        var byCount = blob.Header.IsVariableLengthData
                            ? blob.Header.RleEntryCount > 2
                            : !blob.Header.HasBitpackArray || blob.Header.RleEntryCount > 2;

                        var byRuns = blob.LiteralRunCount > 0;

                        if (byCount != byRuns)
                        {
                            disagreements++;

                            if (disagreements <= 6)
                            {
                                _lines.Add($"DISAGREE {allocationUnit.TableName} col{segment.Key.ColumnId} "
                                           + $"entries {blob.Header.RleEntryCount} literalRuns {blob.LiteralRunCount} "
                                           + $"rleType {(int)blob.Header.RleType}");
                            }
                        }

                        if (byRuns)
                        {
                            withRuns++;

                            var shortest = blob.RleEntries.Where(e => e.IsValue && e.Count > 0).Min(e => e.Count);

                            shortestRun = Math.Min(shortestRun, shortest);
                        }
                    }
                    catch (Exception exception)
                    {
                        _lines.Add($"{allocationUnit.TableName} col{segment.Key.ColumnId}: {exception.Message}");
                    }
                }
            }
        }

        _lines.Add($"examined {examined} segments");

        _lines.Add($"smallest RLE Array Count seen {minimumArrayCount}");

        _lines.Add($"{withRuns} segments carry value runs, shortest run {shortestRun}");

        _lines.Add($"entry count test against run count: {disagreements} disagreements");

        ProbeDump.Write("rle_presence_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
