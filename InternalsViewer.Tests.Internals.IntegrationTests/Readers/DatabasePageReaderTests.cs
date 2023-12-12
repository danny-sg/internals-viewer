using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Tests.Internals.IntegrationTests.Helpers;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Readers;

public class DatabasePageReaderTests
{
    [Fact]
    public void Can_Read_Database_Page()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var reader = new DatabasePageReader(connectionString, new PageAddress(1, 1), 1);

        reader.Load();

        Assert.NotNull(reader.Data);
    }
}