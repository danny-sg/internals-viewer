using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Readers;

public class DatabasePageReaderTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; set; } = testOutputHelper;

    [Fact]
    public async Task Can_Read_Database_Page()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "AdventureWorks2022" };

        var reader = new DatabasePageReader(connection);

        var result = await reader.Read(connection.DatabaseName, new PageAddress(1, 1));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Can_Read_Mdf_Page()
    {
        var service = ServiceHelper.CreateMdfPageService(TestOutputHelper);

        var result = await service.GetPage(new Engine.Database.DatabaseDetail { Name = "TestDatabase" }, 
                                           new PageAddress(1, 9));
    }

}