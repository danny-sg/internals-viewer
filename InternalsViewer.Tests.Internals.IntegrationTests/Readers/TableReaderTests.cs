using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Metadata.Internals.Tables;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services.Loaders;
using InternalsViewer.Tests.Internals.IntegrationTests.Helpers;
using Moq;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Readers;
public class TableReaderTests
{
    [Fact]
    public async  Task Can_Read_Table()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "AdventureWorks2022" };

        var reader = new DatabasePageReader(connection);

        var compressionInfoMock = new Mock<ICompressionInfoService>();

        var service = new PageService(reader, compressionInfoMock.Object);

        var dataReader = new TableReader(service);

        var database = new Database { Name = "AdventureWorks2022" };

        var tableStructure = InternalAllocationUnit.GetAllocationUnit();

        var results = await dataReader.Read(database, new InternalsViewer.Internals.Engine.Address.PageAddress(1, 20), tableStructure);
    }
}
