using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Providers.Metadata;
using System.Diagnostics;
using InternalsViewer.Internals.Interfaces.Connections;

namespace InternalsViewer.Internals.Services.Loaders.Engine;

/// <summary>
/// Service responsible for loading Databases
/// </summary>
public class DatabaseService(ILogger<DatabaseService> logger,
                             IMetadataLoader metadataLoader,
                             IPageService pageService,
                             IAllocationChainService allocationChainService,
                             IIamChainService iamChainService,
                             IPfsChainService pfsChainService)
    : IDatabaseService
{
    private ILogger<DatabaseService> Logger { get; } = logger;

    private IMetadataLoader MetadataLoader { get; } = metadataLoader;

    private IPageService PageService { get; } = pageService;

    private IAllocationChainService AllocationChainService { get; } = allocationChainService;

    private IIamChainService IamChainService { get; } = iamChainService;

    private IPfsChainService PfsChainService { get; } = pfsChainService;

    /// <summary>
    /// Create and load a Database object for the given database name
    /// </summary>
    public async Task<DatabaseSource> LoadAsync(string name, IConnectionType connection)
    {
        Logger.LogInformation($"Loading database {name}");

        Logger.LogDebug("Getting database information");

        var database = new DatabaseSource(connection)
        {
            DatabaseId = 1,
            Name = name
        };

        Logger.LogDebug("Loading Boot Page");

        database.BootPage = await PageService.GetPage<BootPage>(database, BootPage.BootPageAddress);

        Logger.LogDebug("Reading database internal tables/metadata");

        await RefreshMetadata(database);

        Logger.LogDebug("Getting allocation units from metadata");

        database.AllocationUnits = AllocationUnitProvider.GetAllocationUnits(database.Metadata);

        Logger.LogDebug("Getting files from metadata");

        database.Files = FileProvider.GetFiles(database.Metadata);

        Logger.LogDebug("Refreshing allocations");

        await RefreshAllocations(database);

        return database;
    }

    private async Task RefreshMetadata(DatabaseSource database)
    {
        var metadata = await MetadataLoader.Load(database);

        database.Metadata = metadata;
    }

    /// <summary>
    /// Refresh the allocation chains/bitmaps for files and allocation units
    /// </summary>
    public async Task RefreshAllocations(DatabaseSource database)
    {
        await RefreshFileAllocations(database);

        await RefreshPfs(database);

        await RefreshAllocationUnitAllocations(database);
    }

    private async Task RefreshFileAllocations(DatabaseSource databaseDetail)
    {
        Logger.LogDebug("Refreshing file allocations (GAM/SGAM/DCM/BCM)");

        Debug.Assert(databaseDetail.Files.Count > 0);

        databaseDetail.Gam.Clear();
        databaseDetail.SGam.Clear();
        databaseDetail.Dcm.Clear();
        databaseDetail.Bcm.Clear();

        foreach (var file in databaseDetail.Files.Where(f => f.FileType == FileType.Rows))
        {
            Logger.LogTrace("File Allocations: Refreshing File Id {FileId}", file.FileId);

            databaseDetail.Gam.Add(file.FileId,
                await AllocationChainService.LoadChain(databaseDetail, file.FileId, PageType.Gam));

            databaseDetail.SGam.Add(file.FileId,
                await AllocationChainService.LoadChain(databaseDetail, file.FileId, PageType.Sgam));

            databaseDetail.Dcm.Add(file.FileId,
                await AllocationChainService.LoadChain(databaseDetail, file.FileId, PageType.Dcm));

            databaseDetail.Bcm.Add(file.FileId,
                await AllocationChainService.LoadChain(databaseDetail, file.FileId, PageType.Bcm));
        }
    }

    private async Task RefreshAllocationUnitAllocations(DatabaseSource database)
    {
        Logger.LogDebug("Refreshing allocation unit allocations (via IAMs)");

        foreach (var allocationUnit in database.AllocationUnits)
        {
            Logger.LogTrace("Allocation Unit Id: {AllocationUnitId} - Refreshing", allocationUnit.AllocationUnitId);

            if (allocationUnit.FirstIamPage == PageAddress.Empty)
            {
                Logger.LogTrace("Allocation Unit Id: {AllocationUnitId} - No First IAM page", 
                                allocationUnit.AllocationUnitId);

                continue;
            }

            Logger.LogTrace("Allocation Unit Id: {AllocationUnitId} - Loading from First IAM page: {FirstIamPage}", 
                            allocationUnit.AllocationUnitId,
                            allocationUnit.FirstIamPage);

            allocationUnit.IamChain = await IamChainService.LoadChain(database, allocationUnit.FirstIamPage);
        }
    }

    /// <summary>
    /// Refresh the PFS chains for each file
    /// </summary>
    private async Task RefreshPfs(DatabaseSource databaseDetail)
    {
        Logger.LogDebug("Refreshing PFS allocations");

        databaseDetail.Pfs.Clear();

        foreach (var file in databaseDetail.Files.Where(f => f.FileType == FileType.Rows))
        {
            Logger.LogTrace("PFS: Refreshing File Id {FileId}", file.FileId);

            databaseDetail.Pfs.Add(file.FileId, await PfsChainService.LoadChain(databaseDetail, file.FileId));
        }
    }
}
