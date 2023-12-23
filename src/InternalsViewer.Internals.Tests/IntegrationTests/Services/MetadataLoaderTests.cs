using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services;

public class MetadataLoaderTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task Can_Load_Metadata()
    {
        var pageService = ServiceHelper.CreateFilePageService(TestOutputHelper);

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
        Assert.NotEmpty(results.ColumnLayouts);
        Assert.NotEmpty(results.Columns);
        Assert.NotEmpty(results.Files);
    }
}