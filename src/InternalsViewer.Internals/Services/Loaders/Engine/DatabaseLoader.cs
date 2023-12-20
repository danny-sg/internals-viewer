using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Providers.Metadata;
using System.Diagnostics;

namespace InternalsViewer.Internals.Services.Loaders.Engine;

/// <summary>
/// Service responsible for loading Database information
/// </summary>
public class DatabaseLoader(ILogger<DatabaseLoader> logger,
                            IServerInfoProvider serverInfoProvider,
                            IMetadataLoader metadataLoader,
                            IBootPageLoader bootPageLoader,
                            IAllocationChainService allocationChainService,
                            IIamChainService iamChainService,
                            IPfsChainService pfsChainService)
    : IDatabaseLoader
{
    public ILogger<DatabaseLoader> Logger { get; } = logger;

    public IServerInfoProvider ServerInfoProvider { get; } = serverInfoProvider;

    public IMetadataLoader MetadataLoader { get; } = metadataLoader;

    public IBootPageLoader BootPageLoader { get; } = bootPageLoader;

    public IAllocationChainService AllocationChainService { get; } = allocationChainService;

    public IIamChainService IamChainService { get; } = iamChainService;

    public IPfsChainService PfsChainService { get; } = pfsChainService;

    /// <summary>
    /// Create and load a Database object for the given database name
    /// </summary>
    public async Task<DatabaseDetail> Load(string name)
    {
        Logger.LogInformation($"Loading database {name}");

        Logger.LogDebug("Getting database information");

        var databaseInfo = await ServerInfoProvider.GetDatabase(name)
                           ?? throw new ArgumentException($"Database {name} not found");

        var database = new DatabaseDetail
        {
            DatabaseId = databaseInfo.DatabaseId,
            Name = databaseInfo.Name,
            State = databaseInfo.State,
            CompatibilityLevel = databaseInfo.CompatibilityLevel
        };

        Logger.LogDebug("--> {DatabaseInfo}", databaseInfo);

        Logger.LogDebug("Loading Boot Page");

        database.BootPage = await BootPageLoader.GetBootPage(database);

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

    private async Task RefreshMetadata(DatabaseDetail database)
    {
        var metadata = await MetadataLoader.Load(database);

        database.Metadata = metadata;
    }

    /// <summary>
    /// Refresh the allocation chains/bitmaps for each file and allocation type (GAM/SGAM/DCM/BCM)
    /// </summary>
    public async Task RefreshAllocations(DatabaseDetail database)
    {
        await RefreshFileAllocations(database);

        await RefreshAllocationUnitAllocations(database);

        await RefreshPfs(database);
    }

    private async Task RefreshFileAllocations(DatabaseDetail databaseDetail)
    {
        Logger.LogDebug("Refreshing file allocations");

        Debug.Assert(databaseDetail.Files.Count > 0);

        databaseDetail.Gam.Clear();
        databaseDetail.SGam.Clear();
        databaseDetail.Dcm.Clear();
        databaseDetail.Bcm.Clear();

        foreach (var file in databaseDetail.Files.Where(f => f.FileType == FileType.Rows))
        {
            Logger.LogDebug("File Allocations: Refreshing File Id {FileId}", file.FileId);

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

    private async Task RefreshAllocationUnitAllocations(DatabaseDetail databaseDetail)
    {
        Logger.LogDebug("Refreshing allocation unit allocations (via IAMs)");

        foreach (var allocationUnit in databaseDetail.AllocationUnits)
        {
            Logger.LogDebug("Allocation Unit Id: {AllocationUnitId} - Refreshing", allocationUnit.AllocationUnitId);

            if (allocationUnit.FirstIamPage == PageAddress.Empty)
            {
                Logger.LogDebug("Allocation Unit Id: {AllocationUnitId} - No First IAM page", 
                                allocationUnit.AllocationUnitId);

                continue;
            }

            Logger.LogDebug("Allocation Unit Id: {AllocationUnitId} - Loading from First IAM page: {FirstIamPage}", 
                            allocationUnit.AllocationUnitId,
                            allocationUnit.FirstIamPage);

            allocationUnit.IamChain = await IamChainService.LoadChain(databaseDetail, allocationUnit.FirstIamPage);
        }
    }

    /// <summary>
    /// Refresh the PFS chains for each file
    /// </summary>
    private async Task RefreshPfs(DatabaseDetail databaseDetail)
    {
        Logger.LogDebug("Refreshing PFS allocations");

        databaseDetail.Pfs.Clear();

        foreach (var file in databaseDetail.Files.Where(f => f.FileType == FileType.Rows))
        {
            Logger.LogDebug("PFS: Refreshing File Id {FileId}", file.FileId);

            databaseDetail.Pfs.Add(file.FileId, await PfsChainService.LoadChain(databaseDetail, file.FileId));
        }
    }
}
