using InternalsViewer.Connection.BackupFile.Connection;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InternalsViewer.Connection.BackupFile.Tests;

public class DatabaseLoadTests
{
    private const string FullBackupPath = @"C:\Temp\TestBackups\TestDatabase_Full.bak";

    private const string WideWorldImportersBackupPath = @"C:\Temp\TestBackups\WideWorldImporters_Uncompressed.bak";

    [RequiresFileFact(@"C:\Temp\TestBackups\TestDatabase_Full_MultiFile_1.bak")]
    public async Task Can_Load_Database_From_Striped_Backup()
    {
        var host = Host.CreateDefaultBuilder()
                       .UseContentRoot(AppContext.BaseDirectory)
                       .ConfigureServices((_, services) => services.RegisterServices())
                       .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var stripes = Enumerable.Range(1, 4)
                                .Select(n => $@"C:\Temp\TestBackups\TestDatabase_Full_MultiFile_{n}.bak")
                                .ToList();

        var connection = new BackupConnectionFactory().Create(c => c.Filenames = stripes);

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        Assert.NotNull(database.BootPage);
        Assert.StartsWith("TestDatabase", database.BootPage.DatabaseName);

        Assert.NotEmpty(database.Metadata.Files);
        Assert.NotEmpty(database.AllocationUnits);
        Assert.NotEmpty(database.Gam);
        Assert.NotEmpty(database.Pfs);
    }

    [RequiresFileFact(WideWorldImportersBackupPath)]
    public async Task Can_Load_Database_From_WideWorldImporters_Backup()
    {
        var host = Host.CreateDefaultBuilder()
                       .UseContentRoot(AppContext.BaseDirectory)
                       .ConfigureServices((_, services) => services.RegisterServices())
                       .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var connection = new BackupConnectionFactory().Create(c => c.Filename = WideWorldImportersBackupPath);

        var database = await databaseService.LoadAsync("WideWorldImporters", connection, CancellationToken.None);

        Assert.NotNull(database.BootPage);
        Assert.StartsWith("WideWorldImporters", database.BootPage.DatabaseName);

        Assert.NotEmpty(database.Metadata.Files);
        Assert.NotEmpty(database.AllocationUnits);
        Assert.NotEmpty(database.Gam);
        Assert.NotEmpty(database.Pfs);
    }

    [RequiresFileFact(FullBackupPath)]
    public async Task Can_Load_Database_From_Full_Backup()
    {
        var host = Host.CreateDefaultBuilder()
                       .UseContentRoot(AppContext.BaseDirectory)
                       .ConfigureServices((_, services) => services.RegisterServices())
                       .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var connection = new BackupConnectionFactory().Create(c => c.Filename = FullBackupPath);

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        Assert.NotNull(database.BootPage);
        Assert.StartsWith("TestDatabase", database.BootPage.DatabaseName);

        Assert.NotEmpty(database.Metadata.Files);
        Assert.NotEmpty(database.AllocationUnits);
        Assert.NotEmpty(database.Gam);
        Assert.NotEmpty(database.Pfs);
    }
}
