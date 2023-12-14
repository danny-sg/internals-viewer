using System;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using System.Linq;

namespace InternalsViewer.Internals.Services.Loaders;

/// <summary>
/// Service responsible for loading Database information
/// </summary>
public class DatabaseService(IDatabaseInfoProvider databaseInfoProvider,
                             IDatabaseFileInfoProvider databaseFileInfoProvider,
                             IAllocationChainService allocationChainService,
                             IIamChainService iamChainService,
                             IPfsChainService pfsChainService)
    : IDatabaseService
{
    public IDatabaseInfoProvider DatabaseInfoProvider { get; } = databaseInfoProvider;

    public IDatabaseFileInfoProvider DatabaseFileInfoProvider { get; } = databaseFileInfoProvider;

    public IAllocationChainService AllocationChainService { get; } = allocationChainService;

    public IIamChainService IamChainService { get; } = iamChainService;

    public IPfsChainService PfsChainService { get; } = pfsChainService;

    public async Task<Database> Load(string name)
    {
        var databaseInfo = await DatabaseInfoProvider.GetDatabase(name) ?? throw new ArgumentException($"Database {name} not found");

        var database = new Database
        {
            DatabaseId = databaseInfo.DatabaseId,
            Name = databaseInfo.Name,
            State = databaseInfo.State,
            CompatibilityLevel = databaseInfo.CompatibilityLevel
        };

        var files = await DatabaseFileInfoProvider.GetFiles(name);

        database.Files = files;

        await RefreshAllocation(database);

        await RefreshPfs(database);

        await RefreshAllocationUnits(database);

        return database;
    }

    private async Task RefreshAllocationUnits(Database database)
    {
        var allocationUnits = await DatabaseInfoProvider.GetAllocationUnits();

        database.AllocationUnits.Clear();

        foreach (var allocationUnit in allocationUnits.Where(a => a.FirstIamPage.PageId > 0))
        {
            allocationUnit.IamChain = await IamChainService.LoadChain(database, allocationUnit.FirstIamPage);
        }

        database.AllocationUnits.AddRange(allocationUnits);
    }

    private async Task RefreshPfs(Database database)
    {
        database.Pfs.Clear();

        foreach (var file in database.Files)
        {
            database.Pfs.Add(file.FileId, await PfsChainService.LoadChain(database, file.FileId));
        }
    }

    public async Task RefreshAllocation(Database database)
    {
        database.Gam.Clear();
        database.SGam.Clear();
        database.Dcm.Clear();
        database.Bcm.Clear();

        foreach (var file in database.Files)
        {
            database.Gam.Add(file.FileId, await AllocationChainService.LoadChain(database, file.FileId, Engine.Pages.PageType.Gam));
            database.SGam.Add(file.FileId, await AllocationChainService.LoadChain(database, file.FileId, Engine.Pages.PageType.Sgam));
            database.Dcm.Add(file.FileId, await AllocationChainService.LoadChain(database, file.FileId, Engine.Pages.PageType.Dcm));
            database.Bcm.Add(file.FileId, await AllocationChainService.LoadChain(database, file.FileId, Engine.Pages.PageType.Bcm));
        }
    }
}
