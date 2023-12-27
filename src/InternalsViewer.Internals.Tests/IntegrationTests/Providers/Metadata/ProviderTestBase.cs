using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class ProviderTestBase(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    protected async Task<InternalMetadata> GetMetadata()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "TestDatabase" };

        var pageService = ServiceHelper.CreateDataFilePageService(TestOutput);

        var dataReader = new RecordReader(pageService);

        var database = new DatabaseDetail
        {
            Name = "TestDatabase",
            BootPage = new BootPage { FirstAllocationUnitsPage = new PageAddress(1, 20) }
        };

        var service = new MetadataLoader(TestLogger.GetLogger<MetadataLoader>(TestOutput), dataReader);

        var metadata = await service.Load(database);

        return metadata;
    }
}