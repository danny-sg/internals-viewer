using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Readers;

public class QueryPageReaderTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; set; } = testOutputHelper;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    [RequiresConnectionStringFact("local")]
    public async Task Can_Read_Database_Page()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var reader = new QueryPageReader(NullLogger<QueryPageReader>.Instance, connectionString);

        var result = await reader.Read("TestDatabase", new PageAddress(1, 1));

        Assert.NotNull(result);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Can_Read_Mdf_Page()
    {
        var service = ServiceHelper.CreatePageService(TestOutputHelper);

        var connection = new FileConnectionFactory().Create(c => c.Filename = "./IntegrationTests/Test Data/TestDatabase.mdf");

        var result = await service.GetPage(new DatabaseSource(connection), 
                                           new PageAddress(1, 9));

        Assert.Equal(PageType.Boot, result.PageHeader.PageType);  
    }
}