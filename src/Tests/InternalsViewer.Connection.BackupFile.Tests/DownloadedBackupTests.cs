using System.Text;
using InternalsViewer.Connection.BackupFile.Connection;
using InternalsViewer.Connection.BackupFile.Content;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace InternalsViewer.Connection.BackupFile.Tests;

/// <summary>
/// Compressed backups produced elsewhere rather than created by these tests
/// </summary>
public class DownloadedBackupTests(ITestOutputHelper testOutput)
{
    private const string AdventureWorksPath = @"C:\Temp\AdventureWorks2025.bak";

    private const string AdventureWorksDwPath = @"C:\Temp\TestBackups\AdventureWorksDW2025 (1).bak";

    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(AdventureWorksPath)]
    public void AdventureWorks_Decodes_To_An_Mtf_Stream()
    {
        Report(AdventureWorksPath);
    }

    [RequiresFileFact(AdventureWorksDwPath)]
    public void AdventureWorksDw_Decodes_To_An_Mtf_Stream()
    {
        Report(AdventureWorksDwPath);
    }

    [RequiresFileFact(AdventureWorksPath)]
    public async Task Can_Load_Database_From_AdventureWorks_Backup()
    {
        var host = Host.CreateDefaultBuilder()
                       .UseContentRoot(AppContext.BaseDirectory)
                       .ConfigureServices((_, services) => services.RegisterServices())
                       .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var connection = new BackupConnectionFactory().Create(c => c.Filename = AdventureWorksPath);

        var database = await databaseService.LoadAsync("AdventureWorks", connection, CancellationToken.None);

        TestOutput.WriteLine($"database={database.BootPage.DatabaseName.TrimEnd('\0', '†')} " +
                             $"files={database.Metadata.Files.Count} allocationUnits={database.AllocationUnits.Count}");

        Assert.NotEmpty(database.Metadata.Files);
        Assert.NotEmpty(database.AllocationUnits);
    }

    private void Report(string path)
    {
        using var content = new CompressedBackupContentSource(path, NullLogger.Instance, CancellationToken.None);

        TestOutput.WriteLine($"{Path.GetFileName(path)}: {content.BlockCount} blocks, {content.Length:N0} bytes, " +
                             $"{content.FailedBlockCount} failed");

        var buffer = new byte[4];

        content.Read(0, buffer);

        TestOutput.WriteLine($"  first block: {Encoding.ASCII.GetString(buffer)}");

        Assert.Equal("TAPE", Encoding.ASCII.GetString(buffer));
    }
}
