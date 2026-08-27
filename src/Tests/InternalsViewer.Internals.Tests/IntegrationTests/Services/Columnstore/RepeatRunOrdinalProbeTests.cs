using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Whether the rows of a repeat run all resolve to the one store ordinal the run names
/// </summary>
public sealed class RepeatRunOrdinalProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Repeat_Run_Ordinals()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == "SegTwoReads");

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        var rowGroup = index.CompressedRowGroups.First();

        var segment = rowGroup.Segments.First(s => s.Key.ColumnId == 4);

        var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

        var stream = new SegmentDataIdStream(blob);

        var store = blob.VariableLengthData!;

        foreach (var row in new[] { 0, 1, 2, 5000, 9998, 9999, 10000, 10001, 59999 })
        {
            var source = stream.GetRowDataIdSource(row);

            _lines.Add($"row {row,6} origin {source.Origin} entry {source.EntryIndex} "
                       + $"valueOrdinal {source.SourceIndex,6} address {store.GetPageSlot(source.SourceIndex)}");
        }

        ProbeDump.Write("repeat_run_ordinal_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
