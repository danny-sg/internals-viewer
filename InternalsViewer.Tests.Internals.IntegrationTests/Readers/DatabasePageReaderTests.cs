using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Tests.Internals.IntegrationTests.TestHelpers;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Readers;

public class DatabasePageReaderTests
{
    [Fact]
    public async Task Can_Read_Database_Page()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "AdventureWorks2022" };

        var reader = new DatabasePageReader(connection);

        var result = await reader.Read(connection.DatabaseName, new PageAddress(1, 1));

        Assert.NotNull(result);
    }
}