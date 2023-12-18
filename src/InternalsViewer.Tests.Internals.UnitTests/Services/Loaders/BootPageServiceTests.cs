using System;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Services.Loaders.Pages;
using InternalsViewer.Tests.Internals.UnitTests.TestHelpers.TestReaders;
using Moq;
// ReSharper disable CommentTypo

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Loaders;

public class BootPageServiceTests
{
    [Fact]
    public async Task Can_Load_Page()
    {
        var filePath = "./Test Data/Test Pages/";

        var reader = new FilePageReader(filePath);

        var compressionInfoService = new Mock<ICompressionInfoService>();

        var pageService = new PageService(reader,
                                          compressionInfoService.Object);

        var database = new DatabaseDetail { Name = "TestDatabase" };

        var service = new BootPageService(pageService);

        var page = await service.GetBootPage(database);

        //dbi_version = 957
        Assert.Equal(957, page.CurrentVersion);
        
        //dbi_createVersion = 904
        Assert.Equal(904, page.CreatedVersion);

        // dbi_cmptlevel = 160
        Assert.Equal(160, page.CompatibilityLevel);

        // dbi_dbid = 9
        Assert.Equal(9, page.DatabaseId);

        // dbi_maxLogSpaceUsed = 19361792
        Assert.Equal(19361792, page.MaxLogSpaceUsed);

        // dbi_crdate = 2023 - 12 - 12 12:26:16.003
        Assert.Equal(new DateTime(2023, 12, 12, 12, 26, 16, 3), page.CreatedDateTime);
        
        // dbi_dbname = AdventureWorks2022
        Assert.Equal("AdventureWorks2022", page.DatabaseName);

        // dbi_collation = 872468488 
        Assert.Equal(872468488, page.Collation);

        // dbi_status = 0x00810008
        Assert.Equal(0x00810008, page.Status);

        Assert.Equal(new PageAddress(1, 20), page.FirstAllocationUnitsPage);

        Assert.Equal(1099511628357, page.NextAllocationUnitId);

        //Assert.Equal(LogSequenceNumberParser.Parse("53:29975:37"), page.CheckpointLsn);
    }
}