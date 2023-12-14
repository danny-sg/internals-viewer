using System.Collections.Generic;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Services.Loaders;
using Moq;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Loaders;

public class DatabaseServiceTests
{
    [Fact]
    public async Task Get_Database_Creates_A_Database()
    {
        // Create a database service using mocks
        var databaseInfoProvider = new Mock<IDatabaseInfoProvider>();
        var databaseFileInfoProvider = new Mock<IDatabaseFileInfoProvider>();
        var allocationChainService = new Mock<IAllocationChainService>();
        var pfsChainService = new Mock<IPfsChainService>();

        var databaseInfo = new DatabaseInfo
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

        databaseFileInfoProvider.Setup(d => d.GetFiles("TestDatabase"))
                                .ReturnsAsync(files);

        databaseInfoProvider.Setup(d => d.GetDatabase("TestDatabase"))
                            .ReturnsAsync(databaseInfo);

        var databaseService = new DatabaseService(databaseInfoProvider.Object,
                                                  databaseFileInfoProvider.Object,
                                                  allocationChainService.Object,
                                                  pfsChainService.Object);

        var result = await databaseService.Load("TestDatabase");

        Assert.NotNull(result);
        Assert.Equal("TestDatabase", result.Name);
        Assert.Equal(170, result.CompatibilityLevel);
        Assert.Equal    (2, result.Files.Count);
        Assert.Equal(DatabaseState.Online, result.State);
    }

    [Fact]
    public async Task Get_Database_Adds_Allocation_Chains()
    {
        // Create a database service using mocks
        var databaseInfoProvider = new Mock<IDatabaseInfoProvider>();
        var databaseFileInfoProvider = new Mock<IDatabaseFileInfoProvider>();
        var allocationChainService = new Mock<IAllocationChainService>();
        var pfsChainService = new Mock<IPfsChainService>();

        var databaseInfo = new DatabaseInfo
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

        databaseFileInfoProvider.Setup(d => d.GetFiles("TestDatabase"))
            .ReturnsAsync(files);

        databaseInfoProvider.Setup(d => d.GetDatabase("TestDatabase"))
            .ReturnsAsync(databaseInfo);

        allocationChainService.Setup(a => a.LoadChain(It.IsAny<Database>(), It.IsAny<short>(), It.IsAny<PageType>()))
            .ReturnsAsync(new AllocationChain());

        var databaseService = new DatabaseService(databaseInfoProvider.Object,
                                                  databaseFileInfoProvider.Object,
                                                  allocationChainService.Object,
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
        var databaseInfoProvider = new Mock<IDatabaseInfoProvider>();
        var databaseFileInfoProvider = new Mock<IDatabaseFileInfoProvider>();
        var allocationChainService = new Mock<IAllocationChainService>();
        var pfsChainService = new Mock<IPfsChainService>();

        var databaseInfo = new DatabaseInfo
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

        databaseFileInfoProvider.Setup(d => d.GetFiles("TestDatabase"))
            .ReturnsAsync(files);

        databaseInfoProvider.Setup(d => d.GetDatabase("TestDatabase"))
            .ReturnsAsync(databaseInfo);

        pfsChainService.Setup(a => a.LoadChain(It.IsAny<Database>(), It.IsAny<short>()))
            .ReturnsAsync(new PfsChain());

        var databaseService = new DatabaseService(databaseInfoProvider.Object,
                                                  databaseFileInfoProvider.Object,
                                                  allocationChainService.Object,
                                                  pfsChainService.Object);

        var result = await databaseService.Load("TestDatabase");

        // Check that all PFS chains are populated for the two files
        Assert.NotNull(result.Pfs[1]);
        Assert.NotNull(result.Pfs[2]);
    }
}