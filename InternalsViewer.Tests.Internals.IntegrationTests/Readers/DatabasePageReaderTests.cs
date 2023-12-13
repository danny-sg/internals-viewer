using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Tests.Internals.IntegrationTests.Helpers;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Readers;

public class DatabasePageReaderTests
{
    [Fact]
    public async Task Can_Read_Database_Page()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var currentConnection = new CurrentConnection(connectionString, "AdventureWorks2022");

        var reader = new DatabasePageReader(currentConnection);

        var result = await reader.Read(currentConnection.DatabaseName, new PageAddress(1, 1));

        Assert.NotNull(result.Data);
    }
}