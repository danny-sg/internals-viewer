using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;
using Xunit.Sdk;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Loaders.Engine;

public class MetadataLoaderTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task Can_Load_Metadata()
    {
        var pageService = ServiceHelper.CreateDataFilePageService(TestOutputHelper);

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(testOutputHelper), pageService);

        var database = new DatabaseDetail
        {
            Name = "TestDatabase",
            BootPage = new BootPage { FirstAllocationUnitsPage = new PageAddress(1, 20) }
        };

        var service = new MetadataLoader(TestLogger.GetLogger<MetadataLoader>(TestOutputHelper), dataReader);

        var results = await service.Load(database);

        Assert.NotEmpty(results.AllocationUnits);
        Assert.NotEmpty(results.RowSets);
        Assert.NotEmpty(results.Indexes);
        Assert.NotEmpty(results.IndexColumns);
        Assert.NotEmpty(results.ColumnLayouts);
        Assert.NotEmpty(results.Columns);
        Assert.NotEmpty(results.Files);
    }
}