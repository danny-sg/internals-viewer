using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.Readers;

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