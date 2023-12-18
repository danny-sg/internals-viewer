using System;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Services.Loaders;
using InternalsViewer.Internals.Services.Loaders.Pages;
using InternalsViewer.Tests.Internals.UnitTests.TestHelpers.TestReaders;
using Moq;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Loaders;

public class AllocationPageServiceTests
{
    [Theory]
    [InlineData(AllocationPage.FirstGamPage, PageType.Gam)]
    [InlineData(AllocationPage.FirstSgamPage, PageType.Sgam)]
    [InlineData(AllocationPage.FirstDcmPage, PageType.Dcm)]
    [InlineData(AllocationPage.FirstBcmPage, PageType.Bcm)]
    public async Task Can_Load_Allocation_Page(int pageId, PageType pageType)
    {
        var service = CreateService();

        var database = new Database { Name = "TestDatabase" };

        var page = await service.Load(database, new PageAddress(1, pageId));

        Assert.Equal(pageType, page.PageHeader.PageType);
    }

    [Theory]
    [InlineData(100)]
    public async Task Can_Load_Iam_Page(int pageId)
    {
        var service = CreateService();

        var database = new Database { Name = "TestDatabase" };

        var page = await service.Load(database, new PageAddress(1, pageId));

        Assert.Equal(PageType.Iam, page.PageHeader.PageType);
        Assert.Equal(new PageAddress(1, 0), page.StartPage);

        Assert.Equal(new PageAddress(1, 99), page.SinglePageSlots[0]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[1]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[2]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[3]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[4]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[5]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[6]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[7]);
    }

    [Fact]
    public async Task Non_Allocation_Page_Throws_Exception()
    {
        var service = CreateService();

        var database = new Database { Name = "TestDatabase" };

        // The boot page (1:9) is not an allocation page
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.Load(database, new PageAddress(1, 9)));
    }

    private static AllocationPageService CreateService()
    {
        var filePath = "./Test Data/Test Pages/";

        var reader = new FilePageReader(filePath);

        var databaseInfoProvider = new Mock<IDatabaseInfoProvider>();
        var structureInfoProvider = new Mock<IStructureInfoProvider>();

        var compressionInfoService = new Mock<ICompressionInfoService>();

        var pageService = new PageService(reader,
                                          compressionInfoService.Object);


        var service = new AllocationPageService(pageService);
        return service;
    }
}