using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Readers;

public class QueryPageReaderTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; set; } = testOutputHelper;

    [Fact]
    public async Task Can_Read_Database_Page()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var reader = new QueryPageReader(connectionString);

        var result = await reader.Read("TestDatabase", new PageAddress(1, 1));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Can_Read_Mdf_Page()
    {
        var service = ServiceHelper.CreateDataFilePageService(TestOutputHelper);

        var result = await service.GetPage(new DatabaseSource(null!) { Name = "TestDatabase" }, 
                                           new PageAddress(1, 9));

        Assert.Equal(PageType.Boot, result.PageHeader.PageType);  
    }
}