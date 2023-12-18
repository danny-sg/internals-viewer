using System;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.Internals.Services.Loaders.Engine;

/// <summary>
/// Service responsible for loading Database information
/// </summary>
public class DatabaseService(ILogger<DatabaseService> logger,
                             IDatabaseInfoProvider databaseInfoProvider,
                             IDatabaseFileInfoProvider databaseFileInfoProvider,
                             IBootPageService bootPageService,
                             IAllocationChainService allocationChainService,
                             IIamChainService iamChainService,
                             IPfsChainService pfsChainService)
    : IDatabaseService
{
    public ILogger<DatabaseService> Logger { get; } = logger;

    public IDatabaseInfoProvider DatabaseInfoProvider { get; } = databaseInfoProvider;

    public IDatabaseFileInfoProvider DatabaseFileInfoProvider { get; } = databaseFileInfoProvider;

    public IBootPageService BootPageService { get; } = bootPageService;

    public IAllocationChainService AllocationChainService { get; } = allocationChainService;

    public IIamChainService IamChainService { get; } = iamChainService;

    public IPfsChainService PfsChainService { get; } = pfsChainService;

    /// <summary>
    /// Create and load a Database object for the given database name
    /// </summary>
    public async Task<DatabaseDetail> Load(string name)
    {
        var databaseInfo = await DatabaseInfoProvider.GetDatabase(name)
                           ?? throw new ArgumentException($"Database {name} not found");

        var database = new DatabaseDetail
        {
            DatabaseId = databaseInfo.DatabaseId,
            Name = databaseInfo.Name,
            State = databaseInfo.State,
            CompatibilityLevel = databaseInfo.CompatibilityLevel
        };

        database.BootPage = await BootPageService.GetBootPage(database);

        var files = await DatabaseFileInfoProvider.GetFiles(name);

        database.Files = files;

        await RefreshAllocation(database);

        await RefreshPfs(database);

        await RefreshAllocationUnits(database);

        return database;
    }

    /// <summary>
    /// Get Allocation Units with their IAM chains
    /// </summary>
    private async Task RefreshAllocationUnits(DatabaseDetail databaseDetail)
    {
        var allocationUnits = await DatabaseInfoProvider.GetAllocationUnits();

        databaseDetail.AllocationUnits.Clear();

        foreach (var allocationUnit in allocationUnits.Where(a => a.FirstIamPage.PageId > 0))
        {
            allocationUnit.IamChain = await IamChainService.LoadChain(databaseDetail, allocationUnit.FirstIamPage);
        }

        databaseDetail.AllocationUnits.AddRange(allocationUnits);
    }

    /// <summary>
    /// Refresh the PFS chains for each file
    /// </summary>
    private async Task RefreshPfs(DatabaseDetail databaseDetail)
    {
        databaseDetail.Pfs.Clear();

        foreach (var file in databaseDetail.Files)
        {
            databaseDetail.Pfs.Add(file.FileId, await PfsChainService.LoadChain(databaseDetail, file.FileId));
        }
    }

    /// <summary>
    /// Refresh the allocation chains/bitmaps for each file and allocation type (GAM/SGAM/DCM/BCM)
    /// </summary>
    public async Task RefreshAllocation(DatabaseDetail databaseDetail)
    {
        databaseDetail.Gam.Clear();
        databaseDetail.SGam.Clear();
        databaseDetail.Dcm.Clear();
        databaseDetail.Bcm.Clear();

        foreach (var file in databaseDetail.Files)
        {
            databaseDetail.Gam.Add(file.FileId, await AllocationChainService.LoadChain(databaseDetail, file.FileId, PageType.Gam));
            databaseDetail.SGam.Add(file.FileId, await AllocationChainService.LoadChain(databaseDetail, file.FileId, PageType.Sgam));
            databaseDetail.Dcm.Add(file.FileId, await AllocationChainService.LoadChain(databaseDetail, file.FileId, PageType.Dcm));
            databaseDetail.Bcm.Add(file.FileId, await AllocationChainService.LoadChain(databaseDetail, file.FileId, PageType.Bcm));
        }
    }
}
