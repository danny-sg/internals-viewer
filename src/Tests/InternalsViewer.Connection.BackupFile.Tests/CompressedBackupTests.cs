using System.Text;
using InternalsViewer.Connection.BackupFile.Compression;
using InternalsViewer.Connection.BackupFile.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace InternalsViewer.Connection.BackupFile.Tests;

public class CompressedBackupTests(ITestOutputHelper testOutput)
{
    private const string CompressedPath = @"C:\Temp\TestBackups\IV2025_Compressed.bak";

    private const string UncompressedPath = @"C:\Temp\TestBackups\IV2025_Uncompressed.bak";

    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(CompressedPath)]
    public void Detects_A_Compressed_Backup()
    {
        Assert.True(CompressedBackupFormat.IsCompressed(CompressedPath));
    }

    [RequiresFileFact(UncompressedPath)]
    public void Does_Not_Detect_An_Uncompressed_Backup()
    {
        Assert.False(CompressedBackupFormat.IsCompressed(UncompressedPath));
    }

    /// <summary>
    /// The content source presents the MTF stream of a compressed backup without expanding it
    /// </summary>
    [RequiresFileFact(CompressedPath)]
    public void Content_Source_Presents_The_Mtf_Stream_Without_Expanding()
    {
        using var content = Open();

        TestOutput.WriteLine($"{content.BlockCount} blocks, {content.Length} bytes, " +
                             $"{content.FailedBlockCount} failed");

        var buffer = new byte[4];

        content.Read(0, buffer);

        Assert.Equal("TAPE", Encoding.ASCII.GetString(buffer));

        content.Read(1024, buffer);

        Assert.Equal("SFMB", Encoding.ASCII.GetString(buffer));

        content.Read(1536, buffer);

        Assert.Equal("SSET", Encoding.ASCII.GetString(buffer));
    }

    /// <summary>
    /// The decoded stream must place the MTF structures at the same offsets as an uncompressed backup of the
    /// same database
    /// </summary>
    /// <remarks>
    /// Byte equality against the pair is not the test - they are separate backup operations, so timestamps,
    /// checksums and GUIDs differ throughout. Structure offsets are what prove the decode is aligned.
    /// </remarks>
    [RequiresFileFact(CompressedPath)]
    public void Mtf_Structures_Land_At_The_Expected_Offsets()
    {
        var decoded = ReadAll();

        var reference = File.ReadAllBytes(UncompressedPath);

        foreach (var tag in new[] { "TAPE", "SFMB", "SSET", "VOLB", "MSCI", "MQCI", "MSDA", "MQDA" })
        {
            var expected = IndexOf(reference, tag);

            var actual = IndexOf(decoded, tag);

            TestOutput.WriteLine($"{tag}: decoded@{actual} expected@{expected}");

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// The MTF block parser reads a compressed backup through the content source with no expansion
    /// </summary>
    [RequiresFileFact(CompressedPath)]
    public void Mtf_Blocks_Parse_Through_The_Content_Source()
    {
        using var content = Open();

        var loader = new Reader.BackupFileLoader(NullLogger<Reader.BackupFileLoader>.Instance,
                                                 new BackupContentStream(content));

        try
        {
            var blocks = loader.Load();

            TestOutput.WriteLine(string.Join(", ", blocks.Select(b => b.BlockType)));

            Assert.NotEmpty(blocks);
        }
        finally
        {
            loader.Reader.Dispose();
        }
    }

    /// <summary>
    /// Reads must be servable in any order, which is what page reads do once indexing is done
    /// </summary>
    /// <remarks>
    /// Going backwards forces a restart from a checkpoint rather than decoding forward, so this also covers the
    /// restart path.
    /// </remarks>
    [RequiresFileFact(CompressedPath)]
    public void Content_Source_Serves_Random_Access_Reads()
    {
        var reference = ReadAll();

        using var content = Open();

        var buffer = new byte[512];

        foreach (var offset in new long[] { 3584, 8680, 1024, 200_000, 7680, 0 })
        {
            content.Read(offset, buffer);

            Assert.Equal(reference.AsSpan((int)offset, buffer.Length).ToArray(), buffer);
        }
    }

    /// <summary>
    /// The decoded stream must match a backup of the same database taken moments later
    /// </summary>
    /// <remarks>
    /// They are separate backup operations so a small number of bytes - timestamps, checksums, GUIDs - differ by
    /// design. The first difference is the TAPE block timestamp at byte 52.
    /// </remarks>
    [RequiresFileFact(CompressedPath)]
    public void Decoded_Stream_Matches_The_Uncompressed_Pair()
    {
        var decoded = ReadAll();

        var reference = File.ReadAllBytes(UncompressedPath);

        var comparable = Math.Min(decoded.Length, reference.Length);

        var matching = 0;

        for (var i = 0; i < comparable; i++)
        {
            if (decoded[i] == reference[i])
            {
                matching++;
            }
        }

        var accuracy = (double)matching / comparable;

        TestOutput.WriteLine($"{matching:N0}/{comparable:N0} bytes match ({accuracy:P2})");

        Assert.True(accuracy > 0.999, $"Only {accuracy:P2} of bytes matched the uncompressed pair");
    }

    private static CompressedBackupContentSource Open()
        => new(CompressedPath, NullLogger.Instance, CancellationToken.None);

    private static byte[] ReadAll()
    {
        using var content = Open();

        var buffer = new byte[content.Length];

        content.Read(0, buffer);

        return buffer;
    }

    private static int IndexOf(byte[] data, string tag)
    {
        var pattern = Encoding.ASCII.GetBytes(tag);

        return data.AsSpan().IndexOf(pattern);
    }
}
