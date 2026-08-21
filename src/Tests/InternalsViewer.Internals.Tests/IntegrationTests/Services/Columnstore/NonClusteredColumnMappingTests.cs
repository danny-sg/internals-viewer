using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class NonClusteredColumnMappingTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Report_Column_Mapping()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        foreach (var allocationUnit in database.AllocationUnits.Values
                                               .Where(a => a.TableName.StartsWith("SegNci"))
                                               .GroupBy(a => (a.TableName, a.IndexName))
                                               .Select(g => g.First()))
        {
            TestOutput.WriteLine($"=== {allocationUnit.TableName} / {allocationUnit.IndexName} "
                                 + $"({allocationUnit.IndexType}) parent {allocationUnit.ParentIndexType} ===");

            try
            {
                var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

                foreach (var column in index.Columns)
                {
                    TestOutput.WriteLine($"  csColumnId {column.ColumnStoreColumnId} -> name '{column.Name}' "
                                         + $"internal {column.IsInternal} "
                                         + $"structureColumnId {column.Structure?.ColumnId.ToString() ?? "(none)"} "
                                         + $"type {column.Structure?.DataType.ToString() ?? "(none)"}");
                }
            }
            catch (Exception exception)
            {
                TestOutput.WriteLine($"  failed: {exception.Message}");
            }
        }
    }
}
