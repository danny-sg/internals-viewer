using InternalsViewer.Connection.BackupFile.Compression.Mapping;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace InternalsViewer.Connection.BackupFile.Tests;

/// <summary>
/// What the block map retains once a compressed backup is open
/// </summary>
/// <remarks>
/// Checkpoints are held for the life of the map, so their size is a permanent cost of having the backup open rather than a transient
/// allocation. A checkpoint only has to carry as much history as a match can reach back into.
/// </remarks>
public class CheckpointMemoryTests(ITestOutputHelper testOutput)
{
    private const string XpressPath = @"C:\Temp\TestBackups\IV2025_Compressed.bak";

    private const string ZstdPath = @"C:\Temp\TestBackups\TestDatabase_ZSTD_Low.bak";

    private const int XpressMaximumMatchOffset = 65535;

    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(XpressPath)]
    public void Xpress_Checkpoints_Hold_No_More_Than_The_Match_Window()
    {
        using var file = File.OpenRead(XpressPath);

        var map = ChunkMapper.Build(file, NullLogger.Instance, CancellationToken.None);

        var total = map.Checkpoints.Sum(c => (long)c.History.Length);

        TestOutput.WriteLine($"{map.Checkpoints.Count} checkpoints, {total:N0} bytes retained, " +
                             $"largest {map.Checkpoints.Max(c => c.History.Length):N0}");

        Assert.All(map.Checkpoints, c => Assert.True(c.History.Length <= XpressMaximumMatchOffset,
                                                    $"Checkpoint held {c.History.Length} bytes"));
    }

    /// <summary>
    /// ZSTD frames are self contained, so a restart needs no history at all
    /// </summary>
    [RequiresFileFact(ZstdPath)]
    public void Zstd_Checkpoints_Hold_Nothing()
    {
        using var file = File.OpenRead(ZstdPath);

        var map = ChunkMapper.Build(file, NullLogger.Instance, CancellationToken.None);

        var total = map.Checkpoints.Sum(c => (long)c.History.Length);

        TestOutput.WriteLine($"{map.Checkpoints.Count} checkpoints, {total:N0} bytes retained");

        Assert.Equal(0, total);
    }
}
