using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Tests.Internals.IntegrationTests.TestHelpers;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Services;

public class MetadataLoaderTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task Can_Load_Metadata()
    {
        var pageService = ServiceHelper.CreatePageService(TestOutputHelper);

        var dataReader = new TableReader(pageService);

        var database = new DatabaseDetail
        {
            Name = "AdventureWorks2022",
            BootPage = new BootPage { FirstAllocationUnitsPage = new PageAddress(1, 20) }
        };

        var service = new MetadataLoader(TestLogger.GetLogger<MetadataLoader>(TestOutputHelper), dataReader);

        var results = await service.Load(database);

        Assert.NotEmpty(results.AllocationUnits);
        Assert.NotEmpty(results.RowSets);
        Assert.NotEmpty(results.Indexes);
        Assert.NotEmpty(results.Columns);
        Assert.NotEmpty(results.Files);
    }
}