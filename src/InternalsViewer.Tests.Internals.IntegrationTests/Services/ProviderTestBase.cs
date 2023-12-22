using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Internals.Services.Pages;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Services.Pages.Parsers;
using InternalsViewer.Tests.Internals.IntegrationTests.TestHelpers;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Services;

public class ProviderTestBase(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    protected async Task<InternalMetadata> GetMetadata()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "AdventureWorks2022" };

        var reader = new DatabasePageReader(connection);

        var pageLoader = new PageLoader(reader);


        var parsers = new IPageParser[] { new DataPageParser(), new IndexPageParser() };

        var pageService = new PageService(TestLogger.GetLogger<PageService>(TestOutput), pageLoader, parsers);

        var dataReader = new TableReader(pageService);

        var database = new DatabaseDetail
        {
            Name = "AdventureWorks2022",
            BootPage = new BootPage { FirstAllocationUnitsPage = new PageAddress(1, 20) }
        };

        var service = new MetadataLoader(TestLogger.GetLogger<MetadataLoader>(TestOutput), dataReader);

        var metadata = await service.Load(database);

        return metadata;
    }
}