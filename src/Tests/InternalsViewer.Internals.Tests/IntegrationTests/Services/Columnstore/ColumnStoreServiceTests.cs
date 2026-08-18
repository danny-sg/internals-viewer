using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Services.Columnstore;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class ColumnstoreServiceTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Can_Get_ColumnStore_Index()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput), pageService, loader);

        var service = new ColumnstoreService(dataReader);

        var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(f => f.TableName == "Sales");

        var index = await service.GetIndex(allocationUnit!, database, CancellationToken.None);

        Assert.NotNull(index);
    }
}
