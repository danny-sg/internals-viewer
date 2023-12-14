using System;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Services.Loaders;
using InternalsViewer.Tests.Internals.UnitTests.Helpers.TestReaders;
using Moq;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Loaders;

public class PfsPageServiceTests(ITestOutputHelper output)
{
    public ITestOutputHelper Output { get; } = output;

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

        var service = new PfsPageService(pageService);

        var page = await service.Load(database, new PageAddress(1, 1));

        Assert.Equal(new PfsByte { Allocated = true, PageSpaceFree = SpaceFree.OneHundredPercent, Iam = false, Mixed = false },
                     page.PfsBytes[0]);

        Assert.Equal(new PfsByte { Allocated = true, PageSpaceFree = SpaceFree.OneHundredPercent, Iam = false, Mixed = false },
            page.PfsBytes[1]);


        Assert.Equal(new PfsByte { Allocated = true, PageSpaceFree = SpaceFree.Empty, Iam = true, Mixed = true },
            page.PfsBytes[100]);
    }

    [Fact]
    public async Task Non_Pfs_Page_Throws_Exception()
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

        var service = new PfsPageService(pageService);

        // The boot page (1:9) is not an allocation page
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.Load(database, new PageAddress(1, 9)));
    }
}