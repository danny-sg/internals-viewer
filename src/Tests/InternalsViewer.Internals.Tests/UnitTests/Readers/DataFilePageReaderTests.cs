using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Internals.Tests.UnitTests.Readers;

[Trait("Category", "Unit")]
[Trait("Area", "Readers")]
public sealed class DataFilePageReaderTests : IDisposable
{
    private readonly string testDirectory;

    public DataFilePageReaderTests()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "InternalsViewer.Tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(testDirectory);
    }

    public void Dispose()
    {
        Directory.Delete(testDirectory, true);
    }

    [Fact]
    public async Task Register_Resolves_Secondary_File_From_Recorded_Path()
    {
        var primaryPath = CreateDataFile("Test.mdf", 1);

        var recordedDirectory = Path.Combine(testDirectory, "Recorded");

        Directory.CreateDirectory(recordedDirectory);

        var secondaryPath = CreateDataFile(@"Recorded\Test_2.ndf", 2, 0x22);

        await using var reader = new DataFilePageReader(primaryPath);

        await reader.RegisterFiles([CreateRowsFile(2, secondaryPath)], CancellationToken.None);

        var page = await reader.Read(string.Empty, new PageAddress(2, 1), CancellationToken.None);

        Assert.All(page, b => Assert.Equal(0x22, b));
    }

    [Fact]
    public async Task Register_Falls_Back_To_Primary_File_Directory()
    {
        var primaryPath = CreateDataFile("Test.mdf", 1);

        CreateDataFile("Test_2.ndf", 2, 0x33);

        var recordedPath = Path.Combine(testDirectory, "Missing", "Test_2.ndf");

        await using var reader = new DataFilePageReader(primaryPath);

        await reader.RegisterFiles([CreateRowsFile(2, recordedPath)], CancellationToken.None);

        var page = await reader.Read(string.Empty, new PageAddress(2, 1), CancellationToken.None);

        Assert.All(page, b => Assert.Equal(0x33, b));
    }

    [Fact]
    public async Task Register_Throws_When_Secondary_File_Not_Found()
    {
        var primaryPath = CreateDataFile("Test.mdf", 1);

        var recordedPath = Path.Combine(testDirectory, "Missing", "Test_2.ndf");

        await using var reader = new DataFilePageReader(primaryPath);

        var exception = await Assert.ThrowsAsync<MissingDataFileException>(
            () => reader.RegisterFiles([CreateRowsFile(2, recordedPath)], CancellationToken.None));

        var missingFile = Assert.Single(exception.MissingFiles);

        Assert.Equal(2, missingFile.FileId);
    }

    [Fact]
    public async Task Register_Rejects_File_With_Non_Matching_Header()
    {
        var primaryPath = CreateDataFile("Test.mdf", 1);

        CreateDataFile("Test_2.ndf", 3);

        var recordedPath = Path.Combine(testDirectory, "Missing", "Test_2.ndf");

        await using var reader = new DataFilePageReader(primaryPath);

        await Assert.ThrowsAsync<MissingDataFileException>(
            () => reader.RegisterFiles([CreateRowsFile(2, recordedPath)], CancellationToken.None));
    }

    [Fact]
    public async Task Register_Ignores_Log_Files()
    {
        var primaryPath = CreateDataFile("Test.mdf", 1);

        var logFile = new DatabaseFile(2)
        {
            FileType = FileType.Log,
            Name = "Test_log",
            PhysicalName = Path.Combine(testDirectory, "Missing", "Test_log.ldf")
        };

        await using var reader = new DataFilePageReader(primaryPath);

        await reader.RegisterFiles([logFile], CancellationToken.None);
    }

    [Fact]
    public async Task Read_Throws_For_Unregistered_File_Id()
    {
        var primaryPath = CreateDataFile("Test.mdf", 1);

        await using var reader = new DataFilePageReader(primaryPath);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.Read(string.Empty, new PageAddress(2, 0), CancellationToken.None));
    }

    private string CreateDataFile(string relativePath, short fileId, byte fill = 0)
    {
        var path = Path.Combine(testDirectory, relativePath);

        var data = new byte[PageData.Size * 2];

        data[1] = (byte)PageType.FileHeader;

        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(32), 0);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), fileId);

        data.AsSpan(PageData.Size).Fill(fill);

        File.WriteAllBytes(path, data);

        return path;
    }

    private static DatabaseFile CreateRowsFile(short fileId, string physicalName)
    {
        return new DatabaseFile(fileId)
        {
            FileType = FileType.Rows,
            Name = $"File{fileId}",
            PhysicalName = physicalName
        };
    }
}
