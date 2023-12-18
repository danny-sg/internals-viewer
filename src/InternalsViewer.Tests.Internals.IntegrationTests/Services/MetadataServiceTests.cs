using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services.Loaders;
using InternalsViewer.Internals.Services.Loaders.Database;
using InternalsViewer.Tests.Internals.IntegrationTests.TestHelpers;
using Moq;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Services;
public class MetadataServiceTests
{
    [Fact]
    public async Task Can_Load_Metadata()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "AdventureWorks2022" };

        var reader = new DatabasePageReader(connection);

        var compressionInfoMock = new Mock<ICompressionInfoService>();

        var pageService = new PageService(reader, compressionInfoMock.Object);

        var dataReader = new TableReader(pageService);

        var database = new Database
        {
            Name = "AdventureWorks2022",
            BootPage = new BootPage { FirstAllocationUnitsPage = new PageAddress(1, 20) }
        };

        var service = new MetadataService(TestLogHelper.GetLogger<MetadataService>(), dataReader);

        await service.BuildMetadata(database);

        Assert.NotEmpty(service.AllocationUnits);
        Assert.NotEmpty(service.RowSets);
    }
}