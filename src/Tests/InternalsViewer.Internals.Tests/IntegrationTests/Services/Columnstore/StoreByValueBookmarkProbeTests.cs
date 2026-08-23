using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class StoreByValueBookmarkProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Probe_Store_By_Value_Bookmarks()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var lines = new List<string>();

        foreach (var allocationUnit in database.AllocationUnits.Values.DistinctBy(a => a.AllocationUnitId))
        {
            ColumnStoreIndexProbe index;

            try
            {
                var loaded = await service.GetIndex(allocationUnit, database, CancellationToken.None);

                index = new ColumnStoreIndexProbe(allocationUnit.TableName, loaded);
            }
            catch
            {
                continue;
            }

            foreach (var rowGroup in index.Index.CompressedRowGroups)
            {
                foreach (var segment in rowGroup.Segments)
                {
                    SegmentBlob blob;

                    try
                    {
                        blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);
                    }
                    catch
                    {
                        continue;
                    }

                    if (blob.StructureType != SegmentStructureType.StoreByValue)
                    {
                        continue;
                    }

                    lines.Add($"{index.TableName} rg{rowGroup.RowGroupId} col{segment.Key.ColumnId} "
                              + $"rows {segment.RowCount} bookmarkCount {blob.BookmarkCount} "
                              + $"bookmarkDistance {blob.BookmarkDistance}");

                    if (blob.VariableLengthData is { } store)
                    {
                        lines.Add($"  store valueCount {store.ValueCount} elementSize {store.ElementSize} "
                                  + $"pageCount {store.PageCount} maxStringSize {store.MaxStringSize} "
                                  + $"pageSizes {string.Join(",", store.PageSizes.Take(8))}");

                        lines.Add($"  page valueCounts {string.Join(",", store.Pages.Take(8).Select(p => p.ValueCount))}");
                    }

                    var distinct = blob.Bookmarks.Distinct().ToList();

                    lines.Add($"    bookmarks read {blob.Bookmarks.Length} distinct {distinct.Count} "
                              + $"| {string.Join(" ", distinct.Take(6).Select(b => $"({b.Position},{b.EndRow})"))}");

                    var start = blob.BookmarkArrayOffset;

                    var length = Math.Min(48, blob.Data.Length - start);

                    lines.Add($"    prologue {blob.PrologueSize} bookmarkOffset {start} "
                              + $"raw {Convert.ToHexString(blob.Data.Span.Slice(start, length))}");

                    lines.Add($"    header raw {Convert.ToHexString(blob.Data.Span[..56])}");
                }
            }
        }

        foreach (var line in lines)
        {
            TestOutput.WriteLine(line);
        }

        File.WriteAllLines(Path.Combine("C:", "ColumnstoreDump", "sbv_bookmark_probe.txt"), lines);
    }

    private sealed record ColumnStoreIndexProbe(string TableName, Internals.Columnstore.Metadata.ColumnStoreIndex Index);
}
