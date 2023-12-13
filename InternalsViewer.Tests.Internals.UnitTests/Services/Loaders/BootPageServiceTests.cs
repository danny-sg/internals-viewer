using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Services.Loaders;
using InternalsViewer.Tests.Internals.UnitTests.Helpers.TestReaders;
using Moq;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Loaders;

public class BootPageServiceTests
{
    [Fact]
    public async Task Can_Load_Page()
    {
        var filePath = "./Test Data/Test Pages/";

        var reader = new FilePageReader(filePath);

        var databaseInfoProvider = new Mock<IDatabaseInfoProvider>();
        var structureInfoProvider = new Mock<IStructureInfoProvider>();

        var compressionInfoService = new Mock<ICompressionInfoService>();

        var pageService = new PageService(databaseInfoProvider.Object,
            structureInfoProvider.Object,
            reader,
            compressionInfoService.Object);

        var database = new Database { Name = "TestDatabase" };

        var service = new BootPageService(pageService);

        var page = await service.Load(database);

        Assert.Equal(new LogSequenceNumber("53:29975:37"), page.CheckpointLsn);
    }
}