using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Which segment of the scaled table the entry width rule gets wrong, and what its header holds
/// </summary>
public sealed class ScaledBlobProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Scaled_Blobs()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == "SegScaled");

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        foreach (var rowGroup in index.CompressedRowGroups)
        {
            foreach (var segment in rowGroup.Segments)
            {
                var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId);

                var label = $"rg{rowGroup.RowGroupId} col{segment.Key.ColumnId} {column?.Name} "
                            + $"[{column?.Structure?.DataType}] enc {(int)segment.Encoding} "
                            + $"base {segment.BaseId} magnitude {segment.Magnitude} max {segment.MaxDataId}";

                try
                {
                    var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                    _lines.Add($"OK   {label} rleType {(int)blob.Header.RleType} "
                               + $"entryBytes {blob.Header.RleEntrySize} arrayCount {blob.Header.RleArrayCount} "
                               + $"entries {blob.Header.RleEntryCount} "
                               + $"bitpackEntrySize {blob.Header.BitpackEntrySize} minId {blob.Header.BitpackMinId} "
                               + $"units {blob.Header.BitpackUnitCount}");
                }
                catch (Exception exception)
                {
                    var header = await service.GetSegmentHeader(database, segment, CancellationToken.None);

                    _lines.Add($"FAIL {label} rleType {(int)header.RleType} "
                               + $"arrayCount {header.RleArrayCount} entrySize {header.RleArrayEntrySize} "
                               + $"bitpackEntrySize {header.BitpackEntrySize} minId {header.BitpackMinId} "
                               + $"units {header.BitpackUnitCount} bookmarks {header.BookmarkCount}");

                    _lines.Add($"     {exception.Message}");
                }
            }
        }

        ProbeDump.Write("scaled_blob_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
