using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Services.Loaders;
using System.Threading.Tasks;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Pages;
using Moq;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Loaders;

public class PageServiceTests
{
    [Fact]
    public async Task Can_Load_Page()
    {
        var databaseInfoProvider = new Mock<IDatabaseInfoProvider>();
        var structureInfoProvider = new Mock<IStructureInfoProvider>();

        var reader = new Mock<IPageReader>();

        var compressionInfoService = new Mock<ICompressionInfoService>();

        var pageService = new PageService(databaseInfoProvider.Object,
                                          structureInfoProvider.Object,
                                          reader.Object,
                                          compressionInfoService.Object);

        var database = new Database();

        var pageAddress = new PageAddress(1, 1);

        var data = new byte[8192];

        reader.Setup(r => r.Read(database.Name, pageAddress))
              .ReturnsAsync(new byte[8192]);

        var page = await pageService.Load<Page>(database, pageAddress);

        Assert.Equal(database, page.Database);
        Assert.Equal(pageAddress, page.PageAddress);
        Assert.Equal(data, page.PageData);
    }

    [Fact]
    public async Task Can_Load_Compressed_Page()
    {
        var databaseInfoProvider = new Mock<IDatabaseInfoProvider>();
        var structureInfoProvider = new Mock<IStructureInfoProvider>();

        var reader = new Mock<IPageReader>();

        var compressionInfoService = new Mock<ICompressionInfoService>();

        structureInfoProvider.Setup(s => s.GetCompressionType(It.IsAny<long>()))
                             .ReturnsAsync(CompressionType.Page);

        var pageService = new PageService(databaseInfoProvider.Object,
                                          structureInfoProvider.Object,
                                          reader.Object,
                                          compressionInfoService.Object);

        var database = new Database();

        var pageAddress = new PageAddress(1, 1);

        var data = new byte[8192];

        reader.Setup(r => r.Read(database.Name, pageAddress))
            .ReturnsAsync(data);

        var page = await pageService.Load<Page>(database, pageAddress);

        // Check if the page loader calls the compression info service
        compressionInfoService.Verify(c => c.Load(page));
    }


    [Fact]
    public void Can_Load_Offset_Table()
    {
        var page = new Page();

        page.PageHeader.SlotCount = 10;

        var offsets = new List<ushort> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        page.PageData = new byte[8192];


        var start = 8192 - (offsets.Count * 2);

        foreach(var offset in offsets)
        {
            var bytes = BitConverter.GetBytes(offset);

            page.PageData[start] = bytes[0];
            page.PageData[start + 1] = bytes[1];

            start += 2;
        }

        PageService.LoadOffsetTable(page);

        Assert.Equivalent(offsets, page.OffsetTable);
    }
}