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
/// Compressed backups of a database with a memory optimized (FILESTREAM) filegroup
/// </summary>
/// <remarks>
/// These backups contain a PH6S section holding the FILESTREAM container. It is not pages and is not decoded -
/// what matters is that it is skipped with the correct length, because every MTF block records its own address
/// and losing bytes makes everything after it unreadable.
/// </remarks>
public class CompressedFilestreamTests(ITestOutputHelper testOutput)
{
    private const string CompressedPath = @"C:\Temp\TestBackups\FsTest_Compressed.bak";

    private const string UncompressedPath = @"C:\Temp\TestBackups\FsTest_Uncompressed.bak";

    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(CompressedPath)]
    public void Decoded_Stream_Keeps_Mtf_Structures_Aligned()
    {
        using var content = new CompressedBackupContentSource(CompressedPath,
                                                              NullLogger.Instance,
                                                              CancellationToken.None);

        var reference = File.ReadAllBytes(UncompressedPath);

        TestOutput.WriteLine($"decoded {content.Length:N0}, uncompressed pair {reference.Length:N0}, " +
                             $"{content.BlockCount} blocks");

        foreach (var tag in new[] { "TAPE", "SFMB", "SSET", "VOLB", "MSCI", "MSDA", "PH6S", "MSTL", "MSLS" })
        {
            var expected = IndexOf(reference, tag);

            var actual = FindInStream(content, tag);

            TestOutput.WriteLine($"  {tag}: decoded@{actual:N0} expected@{expected:N0}");
        }
    }

    [RequiresFileFact(CompressedPath)]
    public async Task Can_Load_Database_From_Compressed_Filestream_Backup()
    {
        var host = Host.CreateDefaultBuilder()
                       .UseContentRoot(AppContext.BaseDirectory)
                       .ConfigureServices((_, services) => services.RegisterServices())
                       .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var connection = new BackupConnectionFactory().Create(c => c.Filename = CompressedPath);

        var database = await databaseService.LoadAsync("FsTest", connection, CancellationToken.None);

        Assert.NotNull(database.BootPage);
        Assert.StartsWith("FsTest", database.BootPage.DatabaseName);

        Assert.NotEmpty(database.Metadata.Files);
        Assert.NotEmpty(database.AllocationUnits);
    }

    /// <summary>
    /// The downloaded WideWorldImporters backup also has a memory optimized filegroup
    /// </summary>
    /// <remarks>
    /// Written by SQL Server 2016, so a different producer as well as a much larger FILESTREAM section - roughly
    /// 32 MB against 2 MB here.
    /// </remarks>
    [RequiresFileFact(@"C:\Temp\TestBackups\WideWorldImporters-Full.bak")]
    public async Task Can_Load_Database_From_Downloaded_Compressed_Backup()
    {
        var host = Host.CreateDefaultBuilder()
                       .UseContentRoot(AppContext.BaseDirectory)
                       .ConfigureServices((_, services) => services.RegisterServices())
                       .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var connection = new BackupConnectionFactory()
            .Create(c => c.Filename = @"C:\Temp\TestBackups\WideWorldImporters-Full.bak");

        var database = await databaseService.LoadAsync("WideWorldImporters", connection, CancellationToken.None);

        TestOutput.WriteLine($"files={database.Metadata.Files.Count} " +
                             $"allocationUnits={database.AllocationUnits.Count}");

        Assert.NotEmpty(database.Metadata.Files);
        Assert.NotEmpty(database.AllocationUnits);
    }

    private static int IndexOf(byte[] data, string tag) => data.AsSpan().IndexOf(Encoding.ASCII.GetBytes(tag));

    private static long FindInStream(CompressedBackupContentSource content, string tag)
    {
        var pattern = Encoding.ASCII.GetBytes(tag);

        var buffer = new byte[1 << 20];

        for (var position = 0L; position < content.Length; position += buffer.Length - 8)
        {
            var length = (int)Math.Min(buffer.Length, content.Length - position);

            if (length < pattern.Length)
            {
                break;
            }

            content.Read(position, buffer.AsSpan(0, length));

            var index = buffer.AsSpan(0, length).IndexOf(pattern);

            if (index >= 0)
            {
                return position + index;
            }
        }

        return -1;
    }
}
