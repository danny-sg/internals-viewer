using System.Text;
using InternalsViewer.Connection.BackupFile.Compression;
using InternalsViewer.Connection.BackupFile.Compression.Decoders;
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
/// Backups compressed with ALGORITHM = ZSTD
/// </summary>
/// <remarks>
/// The container is the same as an MS_XPRESS backup - only the chunk payloads differ, each being a self contained ZSTD frame.
///
/// Every compression level is covered because the level is the setting most likely to change the frame structure, and one that introduced
/// cross chunk history would return wrong bytes rather than failing.
/// </remarks>
public class ZstdBackupTests(ITestOutputHelper testOutput)
{
    private const string LowPath = @"C:\Temp\TestBackups\TestDatabase_ZSTD_Low.bak";

    private const string MediumPath = @"C:\Temp\TestBackups\TestDatabase_ZSTD_Medium.bak";

    private const string HighPath = @"C:\Temp\TestBackups\TestDatabase_ZSTD_High.bak";

    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileTheory(LowPath, MediumPath, HighPath)]
    [InlineData(LowPath)]
    [InlineData(MediumPath)]
    [InlineData(HighPath)]
    public void Decodes_To_An_Mtf_Stream(string path)
    {
        using var content = new CompressedContentSource(path, NullLogger.Instance, CancellationToken.None);

        TestOutput.WriteLine($"{content.ChunkCount} chunks, {content.Length:N0} bytes, {content.FailedChunkCount} failed");

        var buffer = new byte[4];

        content.Read(0, buffer);

        Assert.Equal("TAPE", Encoding.ASCII.GetString(buffer));

        content.Read(1024, buffer);

        Assert.Equal("SFMB", Encoding.ASCII.GetString(buffer));

        content.Read(1536, buffer);

        Assert.Equal("SSET", Encoding.ASCII.GetString(buffer));

        Assert.Equal(0, content.FailedChunkCount);
    }

    /// <summary>
    /// A higher compression level shrinks the file without changing how the container is read
    /// </summary>
    /// <remarks>
    /// The decompressed sizes are close but not equal - these are separate backup operations, so the MTF stream differs by a soft filemark.
    /// That is why this compares compressed sizes and decode health rather than asserting the streams are identical.
    /// </remarks>
    [RequiresFileFact(LowPath, MediumPath, HighPath)]
    public void Higher_Compression_Level_Shrinks_The_File_And_Still_Decodes()
    {
        var paths = new[] { LowPath, MediumPath, HighPath };

        var sources = paths.Select(p => new CompressedContentSource(p, NullLogger.Instance, CancellationToken.None))
                           .ToList();

        try
        {
            foreach (var (path, source) in paths.Zip(sources))
            {
                TestOutput.WriteLine($"{Path.GetFileName(path)}: {new FileInfo(path).Length:N0} compressed, " +
                                     $"{source.ChunkCount} chunks, {source.Length:N0} decompressed");
            }

            var compressedSizes = paths.Select(p => new FileInfo(p).Length).ToList();

            Assert.True(compressedSizes[0] > compressedSizes[1], "Medium should be smaller than Low");

            Assert.True(compressedSizes[1] > compressedSizes[2], "High should be smaller than Medium");

            Assert.All(sources, s => Assert.Equal(0, s.FailedChunkCount));
        }
        finally
        {
            foreach (var source in sources)
            {
                source.Dispose();
            }
        }
    }

    /// <summary>
    /// The declared algorithm selects the decoder, so the payload never has to be sniffed
    /// </summary>
    [RequiresFileTheory(LowPath, MediumPath, HighPath)]
    [InlineData(LowPath)]
    [InlineData(MediumPath)]
    [InlineData(HighPath)]
    public void Header_Declares_Zstd_At_Every_Level(string path)
    {
        using var file = File.OpenRead(path);

        var buffer = new byte[sizeof(uint)];

        file.Position = CompressedBackupFormat.AlgorithmOffset;

        file.ReadExactly(buffer);

        Assert.Equal(CompressionAlgorithm.Zstd, (CompressionAlgorithm)BitConverter.ToUInt32(buffer));

        using var decoder = ChunkDecoderFactory.Create(file);

        Assert.IsType<ZstdChunkDecoder>(decoder);
    }

    [RequiresFileTheory(LowPath, MediumPath, HighPath)]
    [InlineData(LowPath)]
    [InlineData(MediumPath)]
    [InlineData(HighPath)]
    public async Task Can_Load_Database_From_Zstd_Backup(string path)
    {
        var host = Host.CreateDefaultBuilder()
                       .UseContentRoot(AppContext.BaseDirectory)
                       .ConfigureServices((_, services) => services.RegisterServices())
                       .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var connection = new BackupConnectionFactory().Create(c => c.Filename = path);

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        TestOutput.WriteLine($"database={database.BootPage.DatabaseName.TrimEnd('\0', '†')} " +
                             $"files={database.Metadata.Files.Count} " +
                             $"allocationUnits={database.AllocationUnits.Count}");

        Assert.NotEmpty(database.Metadata.Files);
        Assert.NotEmpty(database.AllocationUnits);
        Assert.NotEmpty(database.Gam);
        Assert.NotEmpty(database.Pfs);
    }
}
