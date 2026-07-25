using InternalsViewer.Connection.BackupFile.Mtf.Blocks;
using Xunit.Abstractions;
using InternalsViewer.Connection.BackupFile.Mtf;
using InternalsViewer.Connection.BackupFile.Mtf.Blocks;

namespace InternalsViewer.Connection.BackupFile.Tests;

public class MtfBlockLoaderTests(ITestOutputHelper testOutput) : IDisposable
{
    private const string CompressedBackupPath = @"C:\Temp\AdventureWorks2025.bak";

    private const string WideWorldImportersBackupPath = @"C:\Temp\TestBackups\WideWorldImporters_Uncompressed.bak";

    private readonly List<string> tempFiles = [];

    public ITestOutputHelper TestOutput { get; } = testOutput;

    public void Dispose()
    {
        foreach (var file in tempFiles)
        {
            File.Delete(file);
        }
    }

    private string CreateTempFile(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bak");

        File.WriteAllBytes(path, content);

        tempFiles.Add(path);

        return path;
    }

    [Fact]
    public void Compressed_Backup_Signature_Throws_NotSupportedException()
    {
        var path = CreateTempFile([.."MSSQLBAK"u8, ..new byte[100]]);

        var loader = new MtfBlockLoader(TestLogger.GetLogger<MtfBlockLoader>(TestOutput), path);

        try
        {
            var exception = Assert.Throws<NotSupportedException>(() => loader.Load());

            Assert.Contains("compressed", exception.Message);
        }
        finally
        {
            loader.Reader.Dispose();
        }
    }

    [Fact]
    public void Non_Backup_File_Throws_InvalidDataException()
    {
        var path = CreateTempFile([.."This is not a backup file"u8]);

        var loader = new MtfBlockLoader(TestLogger.GetLogger<MtfBlockLoader>(TestOutput), path);

        try
        {
            Assert.Throws<InvalidDataException>(() => loader.Load());
        }
        finally
        {
            loader.Reader.Dispose();
        }
    }

    [RequiresFileFact(WideWorldImportersBackupPath)]
    public void Unknown_Sections_Are_Skipped_And_Load_Continues_To_End_Of_Set()
    {
        var loader = new MtfBlockLoader(TestLogger.GetLogger<MtfBlockLoader>(TestOutput), WideWorldImportersBackupPath);

        try
        {
            var blocks = loader.Load();

            var blockTypes = blocks.Select(b => b.BlockType).ToList();

            TestOutput.WriteLine(string.Join(", ", blockTypes));

            Assert.Contains(BlockType.MSDA, blockTypes);
            Assert.Contains(BlockType.MSTL, blockTypes);
            Assert.Equal(BlockType.EndOfSet, blockTypes.Last());
        }
        finally
        {
            loader.Reader.Dispose();
        }
    }

    [RequiresFileFact(CompressedBackupPath)]
    public void Real_Compressed_Backup_Throws_NotSupportedException()
    {
        var loader = new MtfBlockLoader(TestLogger.GetLogger<MtfBlockLoader>(TestOutput), CompressedBackupPath);

        try
        {
            var exception = Assert.Throws<NotSupportedException>(() => loader.Load());

            Assert.Contains("compressed", exception.Message);
        }
        finally
        {
            loader.Reader.Dispose();
        }
    }
}
