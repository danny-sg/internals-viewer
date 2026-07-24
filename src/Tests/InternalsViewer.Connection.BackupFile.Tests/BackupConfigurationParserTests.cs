using InternalsViewer.Connection.BackupFile.Format.Configuration;
using InternalsViewer.Connection.BackupFile.Reader;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalsViewer.Connection.BackupFile.Tests;

public class BackupConfigurationParserTests
{
    private const string FullBackupPath = @"C:\Temp\TestBackups\TestDatabase_Full.bak";

    private const string WideWorldImportersBackupPath = @"C:\Temp\TestBackups\WideWorldImporters_Uncompressed.bak";

    [RequiresFileFact(FullBackupPath)]
    public void Can_Parse_Configuration_From_Full_Backup()
    {
        var configuration = ParseConfiguration(FullBackupPath);

        Assert.Equal("TestDatabase", configuration.DatabaseName);

        var filegroup = Assert.Single(configuration.Filegroups);

        Assert.Equal("PRIMARY", filegroup.Name);
        Assert.Equal(1, filegroup.Ordinal);

        Assert.Equal(2, configuration.Files.Count);

        var dataFile = configuration.Files[0];

        Assert.Equal(1, dataFile.FileId);
        Assert.Equal(BackupFileType.Data, dataFile.FileType);
        Assert.Equal("TestDatabase", dataFile.LogicalName);
        Assert.EndsWith("TestDatabase.mdf", dataFile.PhysicalName);
        Assert.Equal(2240, dataFile.SizeInPages);
        Assert.Equal(1, dataFile.FilegroupOrdinal);

        var logFile = configuration.Files[1];

        Assert.Equal(2, logFile.FileId);
        Assert.Equal(BackupFileType.Log, logFile.FileType);
        Assert.Equal("TestDatabase_log", logFile.LogicalName);
        Assert.EndsWith("TestDatabase_log.ldf", logFile.PhysicalName);
    }

    [RequiresFileFact(WideWorldImportersBackupPath)]
    public void Can_Parse_Configuration_From_WideWorldImporters_Backup()
    {
        var configuration = ParseConfiguration(WideWorldImportersBackupPath);

        Assert.Equal("WideWorldImporters", configuration.DatabaseName);

        Assert.Equal(3, configuration.Filegroups.Count);
        Assert.Equal(["PRIMARY", "USERDATA", "WWI_InMemory_Data"], configuration.Filegroups.Select(f => f.Name));

        Assert.Equal(4, configuration.Files.Count);

        var userData = configuration.Files.Single(f => f.LogicalName == "WWI_UserData");

        Assert.Equal(3, userData.FileId);
        Assert.Equal(BackupFileType.Data, userData.FileType);
        Assert.Equal(2, userData.FilegroupOrdinal);

        var inMemory = configuration.Files.Single(f => f.LogicalName == "WWI_InMemory_Data_1");

        Assert.Equal(65537, inMemory.FileId);
        Assert.Equal(BackupFileType.Filestream, inMemory.FileType);
    }

    private static BackupConfiguration ParseConfiguration(string path)
    {
        var reader = new BackupPageReader(path);

        try
        {
            reader.Initialize(CancellationToken.None).GetAwaiter().GetResult();

            Assert.NotNull(reader.Configuration);

            return reader.Configuration;
        }
        finally
        {
            reader.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
