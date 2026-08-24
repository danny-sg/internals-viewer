using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Dumps the bookmark array of a segment with many runs, so what each entry actually stores can be read off
/// </summary>
public sealed class BookmarkProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Report_Bookmarks()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        foreach (var tableName in new[] { "SegTypes" })
        {
        var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == tableName);

        if (allocationUnit is null)
        {
            continue;
        }

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        foreach (var segment in index.RowGroups.SelectMany(r => r.Segments))
        {
            var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

            if (blob.Bookmarks.Length == 0)
            {
                continue;
            }

            TestOutput.WriteLine($"{tableName} col {segment.Key.ColumnId} {blob.Header.StructureType} "
                                 + $"bitpack {blob.Header.HasBitpackArray} "
                                 + $"rows {blob.RowCount} rle {blob.RleEntries.Length} "
                                 + $"bookmarks {blob.Header.BookmarkCount} every {blob.Header.BookmarkDistance} "
                                 + $"entryBytes {blob.Header.RleEntryBytes}");

            for (var i = 0; i < Math.Min(blob.Bookmarks.Length, 4); i++)
            {
                var bookmark = blob.Bookmarks[i];

                var entryIndex = bookmark.GetRleEntryIndex(blob.Header.RleEntryBytes);

                var cumulative = 0;

                for (var e = 0; e <= entryIndex && e < blob.RleEntries.Length; e++)
                {
                    cumulative += blob.RleEntries[e].Count;
                }

                TestOutput.WriteLine($"       entry {entryIndex} count {blob.RleEntries[entryIndex].Count} "
                                     + $"cumulativeThroughEntry {cumulative}");

                TestOutput.WriteLine($"  [{i,3}] expected row {i * blob.Header.BookmarkDistance,-10} "
                                     + $"position {bookmark.Position,-10} "
                                     + $"entry {bookmark.GetRleEntryIndex(blob.Header.RleEntryBytes),-8} "
                                     + $"endRow {bookmark.EndRow}");
            }

            break;
        }
        }
    }
}
