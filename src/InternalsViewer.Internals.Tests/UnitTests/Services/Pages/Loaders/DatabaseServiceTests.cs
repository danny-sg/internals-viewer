using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Metadata.Internals.Tables;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;
using Moq;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Pages.Loaders;

public class DatabaseServiceTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutputHelper { get; set; } = testOutput;

    private static InternalMetadata BuildMetadataWithFiles()
    {
        var metadata = new InternalMetadata();

        metadata.Files.Add(new InternalFile { FileId = 1, FileType = (byte)FileType.Rows });
        metadata.Files.Add(new InternalFile { FileId = 2, FileType = (byte)FileType.Rows });

        return metadata;
    }

    [Fact]
    public async Task Get_Database_Adds_Allocation_Chains()
    {
        var allocationChainService = new Mock<IAllocationChainService>();
        var iamChainService = new Mock<IIamChainService>();
        var pfsChainService = new Mock<IPfsChainService>();
        var pageService = new Mock<IPageService>();
        var metadataProvider = new Mock<IMetadataLoader>();

        metadataProvider.Setup(m => m.Load(It.IsAny<DatabaseSource>()))
                        .ReturnsAsync(BuildMetadataWithFiles());

        allocationChainService.Setup(a => a.LoadChain(It.IsAny<DatabaseSource>(), It.IsAny<short>(), It.IsAny<PageType>()))
            .ReturnsAsync(new AllocationChain());

        pfsChainService.Setup(a => a.LoadChain(It.IsAny<DatabaseSource>(), It.IsAny<short>()))
            .ReturnsAsync(new PfsChain());

        var databaseService = new DatabaseService(TestLogger.GetLogger<DatabaseService>(TestOutputHelper),
                                                  metadataProvider.Object,
                                                  pageService.Object,
                                                  allocationChainService.Object,
                                                  iamChainService.Object,
                                                  pfsChainService.Object);

        var result = await databaseService.LoadAsync("", null);

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
        var bootPageService = new Mock<IPageService>();
        var allocationChainService = new Mock<IAllocationChainService>();
        var iamChainService = new Mock<IIamChainService>();
        var pfsChainService = new Mock<IPfsChainService>();
        var metadataLoader = new Mock<IMetadataLoader>();

        metadataLoader.Setup(m => m.Load(It.IsAny<DatabaseSource>()))
                      .ReturnsAsync(BuildMetadataWithFiles());

        allocationChainService.Setup(a => a.LoadChain(It.IsAny<DatabaseSource>(), It.IsAny<short>(), It.IsAny<PageType>()))
            .ReturnsAsync(new AllocationChain());

        pfsChainService.Setup(a => a.LoadChain(It.IsAny<DatabaseSource>(), It.IsAny<short>()))
            .ReturnsAsync(new PfsChain());

        var databaseService = new DatabaseService(TestLogger.GetLogger<DatabaseService>(testOutput),
                                                  metadataLoader.Object,
                                                  bootPageService.Object,
                                                  allocationChainService.Object,
                                                  iamChainService.Object,
                                                  pfsChainService.Object);

        var result = await databaseService.LoadAsync("TestDatabase", null);

        Assert.NotNull(result.Pfs[1]);
        Assert.NotNull(result.Pfs[2]);
    }
}