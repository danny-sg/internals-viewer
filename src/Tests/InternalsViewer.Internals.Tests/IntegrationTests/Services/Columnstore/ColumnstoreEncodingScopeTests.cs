using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Reports which tables hold store by value segments, those being the ones with a paged value store
/// </summary>
public sealed class ColumnstoreEncodingScopeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Report_Encodings_Across_Tables()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                          pageService,
                                          loader,
                                          new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(dataReader, new LobDataService(pageService));

        foreach (var allocationUnit in database.AllocationUnits.Values.GroupBy(a => a.TableName).Select(g => g.First()))
        {
            try
            {
                var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

                var segments = index.CompressedRowGroups.SelectMany(r => r.Segments).ToList();

                var storeByValue = segments.Where(s => s.Encoding is SegmentEncoding.StoreByValueBased
                                                                  or SegmentEncoding.StringStoreByValueBased)
                                           .ToList();

                if (storeByValue.Count == 0)
                {
                    continue;
                }

                TestOutput.WriteLine($"{allocationUnit.TableName}: {storeByValue.Count} of {segments.Count} segments");

                foreach (var segment in storeByValue)
                {
                    var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                    TestOutput.WriteLine($"    row group {segment.Key.RowGroupId} column {segment.Key.ColumnId} "
                                         + $"{segment.Encoding} rows {segment.RowCount} "
                                         + $"pages {blob.VariableLengthData?.Pages.Length ?? 0} "
                                         + $"lob {blob.Header.LobType} structure {blob.Header.StructureType} "
                                         + $"store {blob.VariableLengthData?.Header.SubLobType.ToString() ?? "-"} "
                                         + $"pageTypes [{string.Join(", ", (blob.VariableLengthData?.Pages ?? []).Select(x => x.SubLobType.ToString()).Distinct())}]");
                }
            }
            catch
            {
                // Not every allocation unit is a columnstore index
            }
        }
    }
}
