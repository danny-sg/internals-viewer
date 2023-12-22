using System.Collections.Generic;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Tests.Internals.UnitTests.TestHelpers;
using Moq;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Pages.Loaders;

public class DatabaseLoaderTests
{
    [Fact]
    public async Task Get_Database_Creates_A_Database()
    {
        // Create a database service using mocks
        var serverInfoProvider = new Mock<IServerInfoProvider>();
        var allocationChainService = new Mock<IAllocationChainService>();
        var iamChainService = new Mock<IIamChainService>();
        var pfsChainService = new Mock<IPfsChainService>();

        var metadataProvider = new Mock<IMetadataLoader>();

        var pageService = new Mock<IPageService>();

        var databaseInfo = new DatabaseSummary
        {
            DatabaseId = 1,
            Name = "TestDatabase",
            State = DatabaseState.Online,
            CompatibilityLevel = 170,
        };

        var files = new List<DatabaseFile>
        {
            new(1) { Name = "File 1.mdf", PhysicalName = @"C:\TestDatabase_1.mdf", Size = 8192 },
            new(2) { Name = "File 2.mdf", PhysicalName = @"C:\TestDatabase_2.mdf", Size = 8192 },
        };

        serverInfoProvider.Setup(d => d.GetDatabase("TestDatabase"))
                            .ReturnsAsync(databaseInfo);

        var databaseService = new DatabaseLoader(TestLogHelper.GetLogger<DatabaseLoader>(),
                                                 serverInfoProvider.Object,
                                                 metadataProvider.Object,
                                                 pageService.Object,
                                                 allocationChainService.Object,
                                                 iamChainService.Object,
                                                 pfsChainService.Object);

        var result = await databaseService.Load("TestDatabase");

        Assert.NotNull(result);
        Assert.Equal("TestDatabase", result.Name);
        Assert.Equal(170, result.CompatibilityLevel);
        Assert.Equal(2, result.Files.Count);
        Assert.Equal(DatabaseState.Online, result.State);
    }

    [Fact]
    public async Task Get_Database_Adds_Allocation_Chains()
    {
        // Create a database service using mocks
        var databaseInfoProvider = new Mock<IServerInfoProvider>();
        var allocationChainService = new Mock<IAllocationChainService>();
        var iamChainService = new Mock<IIamChainService>();
        var pfsChainService = new Mock<IPfsChainService>();
        var pageService = new Mock<IPageService>();

        var databaseInfo = new DatabaseSummary
        {
            DatabaseId = 1,
            Name = "TestDatabase",
            State = DatabaseState.Online,
            CompatibilityLevel = 170,
        };

        var files = new List<DatabaseFile>
        {
            new(1) { Name = "File 1.mdf", PhysicalName = @"C:\TestDatabase_1.mdf", Size = 8192 },
            new(2) { Name = "File 2.mdf", PhysicalName = @"C:\TestDatabase_2.mdf", Size = 8192 },
        };

        var metadataProvider = new Mock<IMetadataLoader>();

        databaseInfoProvider.Setup(d => d.GetDatabase("TestDatabase"))
            .ReturnsAsync(databaseInfo);

        allocationChainService.Setup(a => a.LoadChain(It.IsAny<DatabaseDetail>(), It.IsAny<short>(), It.IsAny<PageType>()))
            .ReturnsAsync(new AllocationChain());

        var databaseService = new DatabaseLoader(TestLogHelper.GetLogger<DatabaseLoader>(),
                                                  databaseInfoProvider.Object,
                                                  metadataProvider.Object,
                                                  pageService.Object,
                                                  allocationChainService.Object,
                                                  iamChainService.Object,
                                                  pfsChainService.Object);

        var result = await databaseService.Load("TestDatabase");

        // Check that all allocations are populated for the two files
        Assert.NotNull(result.Gam[1]);
        Assert.NotNull(result.Gam[2]);

        Assert.NotNull(result.SGam[1]);
        Assert.NotNull(result.SGam[2]);

        Assert.NotNull(result.Dcm[1]);
        Assert.NotNull(result.Dcm[2]);

        Assert.NotNull(result.Bcm[1]);
        Assert.NotNull(result.Bcm[2]);
    }

    [Fact]
    public async Task Get_Database_Adds_Pfs_Chains()
    {
        // Create a database service using mocks
        var databaseInfoProvider = new Mock<IServerInfoProvider>();

        var bootPageService = new Mock<IPageService>();
        var allocationChainService = new Mock<IAllocationChainService>();
        var iamChainService = new Mock<IIamChainService>();
        var pfsChainService = new Mock<IPfsChainService>();
        var metadataLoader = new Mock<IMetadataLoader>();

        var databaseInfo = new DatabaseSummary
        {
            DatabaseId = 1,
            Name = "TestDatabase",
            State = DatabaseState.Online,
            CompatibilityLevel = 170,
        };

        databaseInfoProvider.Setup(d => d.GetDatabase("TestDatabase"))
            .ReturnsAsync(databaseInfo);

        pfsChainService.Setup(a => a.LoadChain(It.IsAny<DatabaseDetail>(), It.IsAny<short>()))
            .ReturnsAsync(new PfsChain());

        var databaseService = new DatabaseLoader(TestLogHelper.GetLogger<DatabaseLoader>(),
                                                  databaseInfoProvider.Object,
                                                  metadataLoader.Object,
                                                  bootPageService.Object,
                                                  allocationChainService.Object,
                                                  iamChainService.Object,
                                                  pfsChainService.Object);

        var result = await databaseService.Load("TestDatabase");

        // Check that all PFS chains are populated for the two files
        Assert.NotNull(result.Pfs[1]);
        Assert.NotNull(result.Pfs[2]);
    }
}